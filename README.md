# Telegram Storage Backend

Backend C# (.NET 8) que usa o Telegram como storage com PostgreSQL para autenticação de usuários.

## Funcionalidades

- ✅ Autenticação JWT com PostgreSQL
- ✅ Upload/Download de arquivos via Telegram Bot API
- ✅ Orquestração de arquivos com metadados
- ✅ APIs REST para gerenciamento
- ✅ Logging e middleware de exceções
- ✅ Docker e docker-compose

## Configuração

### 1. Criar Bot no Telegram
1. Fale com [@BotFather](https://t.me/botfather)
2. Use `/newbot` e siga as instruções
3. Copie o token do bot
4. Crie um chat/canal para storage e obtenha o ID

### 2. Configurar appsettings.json
```json
{
  "TelegramSettings": {
    "BotToken": "SEU_BOT_TOKEN_AQUI",
    "StorageChatId": "SEU_CHAT_ID_AQUI"
  },
  "JwtSettings": {
    "SecretKey": "SuaChaveSecretaDePeloMenos32Caracteres"
  }
}
```

### 3. Executar com Docker
```bash
docker-compose up -d
```

### 4. Executar migration
```bash
cd TelegramStorage
dotnet ef database update
```

## Endpoints da API

### Autenticação
- `POST /api/auth/register` - Registrar usuário
- `POST /api/auth/login` - Login (retorna JWT)

### Arquivos (requer autenticação)
- `POST /api/files/upload` - Upload de arquivo
- `GET /api/files` - Listar arquivos do usuário
- `GET /api/files/{id}` - Obter informações do arquivo
- `GET /api/files/{id}/download` - Download do arquivo
- `DELETE /api/files/{id}` - Deletar arquivo

## Tecnologias

- .NET 8 Web API
- Entity Framework Core
- PostgreSQL
- Telegram.Bot
- JWT Authentication
- Docker