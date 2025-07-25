#!/bin/bash

# Script de Rollback para TelegramStorage
# Use apenas se houver problemas após o deploy
# Executa: chmod +x rollback.sh && ./rollback.sh

set -e

echo "🔄 Iniciando rollback do TelegramStorage..."

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

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

# Verificar se há backups disponíveis
log_info "Procurando backups disponíveis..."
BACKUP_FILES=$(ls backup_*.sql 2>/dev/null || echo "")

if [ -z "$BACKUP_FILES" ]; then
    log_warning "Nenhum backup encontrado. O rollback será apenas do código."
else
    log_info "Backups encontrados:"
    ls -la backup_*.sql
    echo ""
    read -p "Digite o nome do backup para restaurar (ou ENTER para pular): " BACKUP_CHOICE
fi

# 1. Parar containers atuais
log_info "1. Parando containers atuais..."
docker-compose down
log_success "Containers parados."

# 2. Restaurar backup do banco (se escolhido)
if [ ! -z "$BACKUP_CHOICE" ] && [ -f "$BACKUP_CHOICE" ]; then
    log_info "2. Restaurando backup do banco de dados..."
    
    # Subir apenas o banco
    docker-compose up -d postgres 2>/dev/null || docker-compose up -d db 2>/dev/null
    sleep 5
    
    # Restaurar backup
    log_info "Restaurando $BACKUP_CHOICE..."
    docker-compose exec -T postgres psql -U postgres -d telegram_storage < "$BACKUP_CHOICE" || {
        log_error "Falha ao restaurar backup. Verifique o arquivo e tente manualmente."
        exit 1
    }
    log_success "Banco restaurado a partir do backup."
else
    log_warning "2. Pulando restauração do banco."
fi

# 3. Reverter migration (se necessário)
if [ ! -z "$BACKUP_CHOICE" ]; then
    log_info "3. Revertendo migration AddFileChunking..."
    dotnet ef migrations remove --project TelegramStorage --force || {
        log_warning "Não foi possível reverter a migration automaticamente."
        log_info "Execute manualmente: dotnet ef migrations remove --project TelegramStorage"
    }
else
    log_warning "3. Pulando reversão de migration."
fi

# 4. Checkout para commit anterior (se em git)
if git rev-parse --git-dir > /dev/null 2>&1; then
    log_info "4. Código em repositório git detectado."
    
    # Mostrar commits recentes
    echo "Commits recentes:"
    git log --oneline -5
    echo ""
    
    read -p "Digite o hash do commit para voltar (ou ENTER para manter código atual): " COMMIT_HASH
    
    if [ ! -z "$COMMIT_HASH" ]; then
        log_info "Fazendo checkout para commit $COMMIT_HASH..."
        git checkout "$COMMIT_HASH" || {
            log_error "Falha no checkout. Verifique o hash do commit."
            exit 1
        }
        log_success "Código revertido para commit $COMMIT_HASH"
        
        # Rebuild da imagem com código anterior
        log_info "Fazendo rebuild da imagem com código anterior..."
        docker build -t telegram-storage:rollback .
        
        # Atualizar docker-compose para usar a imagem de rollback
        sed -i.bak 's/telegram-storage:latest/telegram-storage:rollback/g' docker-compose.yml
    fi
else
    log_warning "4. Não é um repositório git. Mantendo código atual."
fi

# 5. Subir serviços
log_info "5. Subindo serviços..."
docker-compose up -d
log_success "Serviços iniciados."

# 6. Aguardar e verificar
log_info "6. Aguardando aplicação ficar pronta..."
sleep 10

# Verificar se está rodando
HEALTH_CHECK_URL="http://localhost:5000/api/files"
STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$HEALTH_CHECK_URL" 2>/dev/null || echo "000")

if [ "$STATUS_CODE" = "401" ] || [ "$STATUS_CODE" = "200" ]; then
    log_success "Aplicação está rodando após rollback! (Status: $STATUS_CODE)"
else
    log_warning "Status HTTP: $STATUS_CODE. Verifique os logs."
fi

# 7. Status final
log_info "7. Status dos containers:"
docker-compose ps

echo ""
log_success "🔄 Rollback concluído!"
echo ""
log_warning "IMPORTANTE:"
echo "  • Verifique se a aplicação está funcionando corretamente"
echo "  • Teste o upload de arquivos pequenos"
echo "  • Monitore os logs: docker-compose logs -f"
echo ""

if [ -f "docker-compose.yml.bak" ]; then
    log_info "Backup do docker-compose.yml salvo como docker-compose.yml.bak"
fi

log_info "Para voltar à versão mais recente:"
echo "  git checkout main  # ou sua branch principal"
echo "  ./deploy.sh"