# 📁 Telegram Storage Backend

Backend C# (.NET 8) que utiliza o Telegram como sistema de armazenamento de arquivos, com PostgreSQL para autenticação e gerenciamento de usuários.

## 🚀 Funcionalidades

- ✅ **Autenticação JWT** com PostgreSQL
- ✅ **Upload/Download** de arquivos via Telegram Bot API
- ✅ **Orquestração de arquivos** com metadados
- ✅ **APIs REST** completas para gerenciamento
- ✅ **Logging** e middleware de tratamento de exceções
- ✅ **Docker** e docker-compose para deploy fácil
- ✅ **Validação** de tipos de arquivo e tamanho
- ✅ **Segurança** com JWT Bearer tokens

## 🏗️ Arquitetura

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Frontend/     │    │   Backend API   │    │   Telegram      │
│   Mobile App    │◄──►│   (.NET 8)      │◄──►│   Storage       │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌─────────────────┐
                       │   PostgreSQL    │
                       │   (Metadata)    │
                       └─────────────────┘
```

## ⚙️ Configuração

### 1. 🤖 Criar Bot no Telegram
1. Acesse [@BotFather](https://t.me/botfather) no Telegram
2. Execute `/newbot` e siga as instruções
3. Copie o **token do bot** gerado
4. Crie um **chat privado** ou **canal** para armazenamento
5. Obtenha o **Chat ID** (use bots como @userinfobot)

### 2. 📝 Configurar appsettings.json

```bash
# Copiar o template
cp TelegramStorage/appsettings.Example.json TelegramStorage/appsettings.json

# Editar com suas configurações
nano TelegramStorage/appsettings.json
```

**Configurações necessárias:**
```json
{
  "TelegramSettings": {
    "BotToken": "SEU_TOKEN_DO_BOT_AQUI",
    "StorageChatId": "SEU_CHAT_ID_AQUI"
  },
  "JwtSettings": {
    "SecretKey": "SuaChaveSecretaDePeloMenos32CaracteresParaSeguranca"
  }
}
```

**🔒 Nunca commite o appsettings.json com chaves reais!**

### 3. 🐳 Executar com Docker (Recomendado)
```bash
# Clonar o repositório
git clone <repo-url>
cd telegram-storage

# Executar com docker-compose
docker-compose up -d

# Verificar logs
docker-compose logs -f telegram-storage
```

### 4. 🛠️ Executar localmente
```bash
cd TelegramStorage

# Restaurar pacotes
dotnet restore

# Executar migrations
dotnet ef database update

# Executar aplicação
dotnet run
```

## 📡 Endpoints da API

### 🔐 Autenticação
| Método | Endpoint | Descrição | Body |
|--------|----------|-----------|------|
| `POST` | `/api/auth/register` | Registrar novo usuário | `{ "username": "user", "email": "user@email.com", "password": "123456" }` |
| `POST` | `/api/auth/login` | Login (retorna JWT) | `{ "email": "user@email.com", "password": "123456" }` |

### 📁 Gerenciamento de Arquivos (Requer JWT)
| Método | Endpoint | Descrição | Headers |
|--------|----------|-----------|---------|
| `POST` | `/api/files/upload` | Upload de arquivo | `Authorization: Bearer <token>` |
| `GET` | `/api/files` | Listar arquivos do usuário | `Authorization: Bearer <token>` |
| `GET` | `/api/files/{id}` | Obter informações do arquivo | `Authorization: Bearer <token>` |
| `GET` | `/api/files/{id}/download` | Download do arquivo | `Authorization: Bearer <token>` |
| `DELETE` | `/api/files/{id}` | Deletar arquivo | `Authorization: Bearer <token>` |

## 📋 Exemplos de Uso

### Registrar usuário
```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "joao",
    "email": "joao@email.com", 
    "password": "123456"
  }'
```

### Login
```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "joao@email.com",
    "password": "123456"
  }'
```

### Upload de arquivo
```bash
curl -X POST http://localhost:8080/api/files/upload \
  -H "Authorization: Bearer <seu-jwt-token>" \
  -F "file=@/caminho/para/arquivo.pdf"
```

### Listar arquivos
```bash
curl -X GET http://localhost:8080/api/files \
  -H "Authorization: Bearer <seu-jwt-token>"
```

## 🏗️ Estrutura do Projeto

```
TelegramStorage/
├── Controllers/         # Controladores da API
│   ├── AuthController.cs
│   └── FilesController.cs
├── Services/           # Lógica de negócio
│   ├── AuthService.cs
│   ├── TelegramService.cs
│   └── FileService.cs
├── Models/             # Entidades do banco
│   ├── User.cs
│   └── FileRecord.cs
├── DTOs/               # Data Transfer Objects
│   ├── LoginDto.cs
│   ├── RegisterDto.cs
│   └── FileResponseDto.cs
├── Data/               # Contexto do EF Core
│   └── TelegramStorageContext.cs
├── Configuration/      # Classes de configuração
│   ├── JwtSettings.cs
│   └── TelegramSettings.cs
├── Middlewares/        # Middlewares customizados
│   └── ExceptionMiddleware.cs
└── Infrastructure/     # Repositórios e serviços
```

## 🛡️ Segurança

- **JWT Authentication** com chaves seguras
- **Validação** de tipos de arquivo permitidos
- **Middleware** de tratamento de exceções
- **Logs** detalhados para auditoria
- **Sanitização** de dados de entrada

## 🔧 Tecnologias Utilizadas

| Tecnologia | Versão | Propósito |
|------------|--------|-----------|
| .NET | 8.0 | Framework web |
| Entity Framework Core | 8.0 | ORM para PostgreSQL |
| PostgreSQL | 15 | Banco de dados |
| Telegram.Bot | 19.0 | API do Telegram |
| JWT Bearer | 8.0 | Autenticação |
| BCrypt.Net | 4.0 | Hash de senhas |
| Docker | - | Containerização |

## 🚨 Limitações

- **Tamanho máximo**: Ilimitado (chunks & reassemble)
- **Tipos permitidos**: Configurável via `AllowedContentTypes`
- **Armazenamento**: Dependente da disponibilidade do Telegram
- **Backup**: Arquivos ficam no chat/canal do Telegram

## 🤝 Contribuição

1. Fork o projeto
2. Crie uma branch (`git checkout -b feature/nova-funcionalidade`)
3. Commit suas mudanças (`git commit -am 'Add nova funcionalidade'`)
4. Push para a branch (`git push origin feature/nova-funcionalidade`)
5. Abra um Pull Request

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

## 📞 Suporte

Para dúvidas ou problemas:
- Abra uma [issue](../../issues)
- Entre em contato via email
- Consulte a [documentação da API](http://localhost:8080/swagger) quando o projeto estiver rodando