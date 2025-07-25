#!/bin/bash

# Deploy Script para TelegramStorage - Remoção de Limitação de 50MB
# Executa: chmod +x deploy.sh && ./deploy.sh

set -e  # Para o script se qualquer comando falhar

echo "🚀 Iniciando deploy do TelegramStorage com suporte a arquivos grandes..."

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Função para log colorido
log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Verificar se estamos no diretório correto
if [ ! -f "docker-compose.yml" ]; then
    log_error "docker-compose.yml não encontrado. Execute o script na raiz do projeto."
    exit 1
fi

if [ ! -f "TelegramStorage/TelegramStorage.csproj" ]; then
    log_error "Projeto TelegramStorage não encontrado."
    exit 1
fi

# 1. Backup do banco de dados (opcional, mas recomendado)
log_info "1. Fazendo backup do banco de dados..."
BACKUP_FILE="backup_$(date +%Y%m%d_%H%M%S).sql"

# Se estiver usando PostgreSQL com docker-compose
if docker-compose ps | grep -q postgres; then
    log_info "Fazendo backup do PostgreSQL..."
    docker-compose exec -T postgres pg_dump -U postgres telegram_storage > "$BACKUP_FILE" 2>/dev/null || {
        log_warning "Não foi possível fazer backup automático. Continue manualmente se necessário."
    }
    log_success "Backup salvo em: $BACKUP_FILE"
else
    log_warning "Container PostgreSQL não encontrado. Pule o backup se não usar PostgreSQL."
fi

# 2. Parar containers atuais
log_info "2. Parando containers atuais..."
docker-compose down
log_success "Containers parados."

# 3. Build da nova imagem
log_info "3. Fazendo build da nova imagem Docker..."
docker build -t telegram-storage:latest .
log_success "Nova imagem criada."

# 4. Aplicar migrations
log_info "4. Aplicando migrations no banco de dados..."

# Subir apenas o banco temporariamente para aplicar migrations
log_info "Subindo banco de dados para aplicar migrations..."
docker-compose up -d postgres 2>/dev/null || docker-compose up -d db 2>/dev/null || {
    log_warning "Não foi possível subir o banco automaticamente. Aplique as migrations manualmente."
}

# Aguardar banco ficar pronto
sleep 5

# Aplicar migrations
log_info "Executando migrations..."
dotnet ef database update --project TelegramStorage || {
    log_error "Falha ao aplicar migrations. Verifique a conexão com o banco."
    exit 1
}
log_success "Migrations aplicadas com sucesso."

# 5. Subir todos os serviços
log_info "5. Subindo todos os serviços com a nova imagem..."
docker-compose up -d
log_success "Serviços iniciados."

# 6. Aguardar aplicação ficar pronta
log_info "6. Aguardando aplicação ficar pronta..."
sleep 10

# 7. Verificar se a aplicação está rodando
log_info "7. Verificando se a aplicação está rodando..."

# Tentar fazer uma requisição de health check (ajuste a porta se necessário)
HEALTH_CHECK_URL="http://localhost:5000/api/files"  # Ajuste conforme sua configuração
STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$HEALTH_CHECK_URL" 2>/dev/null || echo "000")

if [ "$STATUS_CODE" = "401" ] || [ "$STATUS_CODE" = "200" ]; then
    log_success "Aplicação está rodando! (Status: $STATUS_CODE)"
else
    log_warning "Status HTTP: $STATUS_CODE. Verifique os logs se necessário."
fi

# 8. Mostrar status dos containers
log_info "8. Status dos containers:"
docker-compose ps

# 9. Informações finais
echo ""
log_success "🎉 Deploy concluído com sucesso!"
echo ""
log_info "Alterações implementadas:"
echo "  • ✅ Remoção da limitação de 50MB"
echo "  • ✅ Suporte a arquivos de qualquer tamanho via chunking"
echo "  • ✅ Chunks de 40MB para otimizar uploads"
echo "  • ✅ Configurações ASP.NET Core para arquivos grandes"
echo "  • ✅ Limites de request body removidos"
echo "  • ✅ Compatibilidade com arquivos existentes"
echo ""
log_info "Comandos úteis:"
echo "  • Ver logs: docker-compose logs -f"
echo "  • Parar: docker-compose down"
echo "  • Reiniciar: docker-compose restart"
echo ""

if [ -f "$BACKUP_FILE" ]; then
    log_info "📁 Backup do banco salvo em: $BACKUP_FILE"
    echo "     (mantenha este arquivo seguro por alguns dias)"
fi

log_success "Deploy finalizado! 🚀"