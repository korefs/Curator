# 🚀 Deploy - Remoção da Limitação de 50MB

Este guia explica como fazer o deploy da nova versão que remove a limitação de 50MB para arquivos.

## ⚡ Deploy Automático (Recomendado)

```bash
# Executar script de deploy
chmod +x deploy.sh
./deploy.sh
```

O script executa automaticamente:
- ✅ Backup do banco de dados
- ✅ Build da nova imagem Docker
- ✅ Aplicação das migrations
- ✅ Restart dos serviços
- ✅ Verificação de saúde

## 🔧 Deploy Manual

Se preferir executar passo a passo:

### 1. Backup do Banco (Recomendado)
```bash
# PostgreSQL
docker-compose exec postgres pg_dump -U postgres telegram_storage > backup_$(date +%Y%m%d_%H%M%S).sql

# MySQL
docker-compose exec mysql mysqldump -u root -p telegram_storage > backup_$(date +%Y%m%d_%H%M%S).sql
```

### 2. Parar Containers
```bash
docker-compose down
```

### 3. Build Nova Imagem
```bash
docker build -t telegram-storage:latest .
```

### 4. Aplicar Migrations
```bash
# Subir apenas o banco
docker-compose up -d postgres  # ou db

# Aplicar migrations
dotnet ef database update --project TelegramStorage
```

### 5. Subir Todos os Serviços
```bash
docker-compose up -d
```

### 6. Verificar Status
```bash
docker-compose ps
docker-compose logs -f telegram-storage
```

## 🔄 Rollback (Se Necessário)

Em caso de problemas:

```bash
# Rollback automático
chmod +x rollback.sh
./rollback.sh
```

Ou manual:
```bash
# 1. Parar containers
docker-compose down

# 2. Restaurar backup
docker-compose up -d postgres
docker-compose exec -i postgres psql -U postgres -d telegram_storage < seu_backup.sql

# 3. Reverter migration
dotnet ef migrations remove --project TelegramStorage

# 4. Voltar código (se em git)
git checkout <commit-anterior>
docker build -t telegram-storage:latest .

# 5. Subir serviços
docker-compose up -d
```

## ✨ Novos Recursos

Após o deploy, o sistema suportará:

- 📁 **Arquivos ilimitados**: Sem mais limite de 50MB
- ⚡ **Chunking automático**: Arquivos >40MB divididos em partes
- 🔄 **Compatibilidade total**: Arquivos existentes continuam funcionando
- 🚀 **Performance otimizada**: Upload/download em paralelo

## 🧪 Testes Pós-Deploy

Teste essencial:
```bash
# Teste upload de arquivo pequeno (<40MB)
curl -X POST -F "file=@arquivo_pequeno.pdf" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/files/upload

# Teste upload de arquivo grande (>40MB)
curl -X POST -F "file=@arquivo_grande.zip" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/files/upload
```

## 📊 Monitoramento

```bash
# Ver logs em tempo real
docker-compose logs -f

# Status dos containers
docker-compose ps

# Uso de recursos
docker stats

# Verificar espaço em disco
df -h
```

## ⚠️ Considerações Importantes

- **Backup**: Sempre faça backup antes do deploy
- **Espaço**: Arquivos grandes usarão mais espaço no Telegram
- **Rede**: Upload/download de arquivos grandes pode ser mais lento
- **Monitoramento**: Monitore uso de CPU/memória após deploy

## 🆘 Troubleshooting

### Erro de Migration
```bash
dotnet ef migrations remove --project TelegramStorage
dotnet ef migrations add AddFileChunking --project TelegramStorage
dotnet ef database update --project TelegramStorage
```

### Container não inicia
```bash
docker-compose logs telegram-storage
# Verificar logs de erro
```

### Banco não conecta
```bash
docker-compose ps  # Verificar se banco está rodando
docker-compose logs postgres  # Ver logs do banco
```

### Performance lenta
- Verificar espaço em disco
- Monitorar CPU/memória
- Verificar conexão de rede com Telegram

---

**Need help?** Verifique os logs ou abra uma issue no repositório.