# 🚀 Setup Rápido - Telegram Storage

## ⚡ Configuração em 3 passos

### 1. 🤖 Criar Bot no Telegram
```bash
# 1. Acesse @BotFather no Telegram
# 2. Execute: /newbot
# 3. Copie o TOKEN gerado
# 4. Crie um canal/chat privado para storage
# 5. Obtenha o CHAT_ID (use @userinfobot)
```

### 2. 📝 Configurar credenciais
```bash
# Copiar template
cp TelegramStorage/appsettings.Example.json TelegramStorage/appsettings.json

# Editar com suas chaves
nano TelegramStorage/appsettings.json
```

**Substitua apenas estes valores:**
- `"BotToken": "SEU_TOKEN_AQUI"`
- `"StorageChatId": "SEU_CHAT_ID_AQUI"`

### 3. 🐳 Executar
```bash
# Deploy automático
./deploy.sh

# Ou manual
docker-compose up -d
```

## ✅ Testar

```bash
# 1. Registrar usuário
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"test","email":"test@test.com","password":"123456"}'

# 2. Login
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"123456"}'

# 3. Upload (substitua TOKEN)
curl -X POST http://localhost:8080/api/files/upload \
  -H "Authorization: Bearer SEU_TOKEN" \
  -F "file=@arquivo.pdf"
```

## 🎉 Funcionalidades

- ✅ **Arquivos ilimitados** (chunks automáticos)
- ✅ **Upload/Download** via Telegram
- ✅ **Autenticação JWT**
- ✅ **API REST** completa

## 🔧 Comandos úteis

```bash
# Ver logs
docker-compose logs -f

# Parar
docker-compose down

# Backup
docker-compose exec postgres pg_dump -U postgres telegram_storage > backup.sql
```

**🔒 IMPORTANTE**: Nunca commite o `appsettings.json` com chaves reais!