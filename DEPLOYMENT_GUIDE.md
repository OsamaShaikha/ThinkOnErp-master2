# Deployment Guide

## Quick Deployment to Server

This guide shows you how to deploy your ThinkOnErp API to your server.

### Prerequisites

1. Git installed on your local machine
2. SSH access to your server
3. Your code pushed to a Git repository (GitHub, GitLab, etc.)

### Option 1: Automated Deployment (Recommended)

Use the automated PowerShell script:

```powershell
# Basic deployment
./deploy-to-server.ps1 -ServerIP "YOUR_SERVER_IP"

# With custom settings
./deploy-to-server.ps1 -ServerIP "YOUR_SERVER_IP" -ServerUser "root" -Branch "main"

# Simple deployment (without nginx)
./deploy-to-server.ps1 -ServerIP "YOUR_SERVER_IP" -SimpleDeployment

# Skip Git push (if already pushed)
./deploy-to-server.ps1 -ServerIP "YOUR_SERVER_IP" -SkipGitPush
```

The script will:
1. Commit and push your local changes to Git
2. SSH into your server
3. Clone or pull the latest code
4. Run the deployment script
5. Start the application with Docker

### Option 2: Manual Deployment

#### Step 1: Push to Git

```bash
# Add all changes
git add .

# Commit
git commit -m "Your commit message"

# Push to remote
git push origin main
```

#### Step 2: Deploy on Server

SSH into your server:

```bash
ssh root@YOUR_SERVER_IP
```

Then run these commands:

```bash
# Clone repository (first time only)
git clone YOUR_REPO_URL ThinkOnErp
cd ThinkOnErp

# Or pull latest changes (if already cloned)
cd ThinkOnErp
git pull origin main

# Make deployment script executable
chmod +x deploy-simple.sh

# Run deployment
./deploy-simple.sh
```

### Deployment Scripts

Two deployment options are available:

1. **Simple Deployment** (`deploy-simple.sh`)
   - API only
   - No nginx reverse proxy
   - Direct access on ports 5160 (HTTP) and 7136 (HTTPS)

2. **Full Deployment** (`deploy.sh`)
   - API + nginx reverse proxy
   - Production-ready setup
   - Access through nginx on port 80/443

### After Deployment

Your API will be available at:
- Simple: `http://YOUR_SERVER_IP:5160`
- Full: `http://YOUR_SERVER_IP` (through nginx)

### Troubleshooting

**Check if containers are running:**
```bash
docker ps
```

**View logs:**
```bash
docker logs thinkonerp-api
```

**Restart containers:**
```bash
docker-compose restart
```

**Stop containers:**
```bash
docker-compose down
```

### Environment Variables

Before first deployment, create `.env` file on your server:

```bash
cd ThinkOnErp
cp .env.example .env
nano .env
```

Update the connection string and JWT settings.

### Database Setup

Don't forget to run the database scripts on your Oracle database:

```sql
@Database/Scripts/01_Create_Sequences.sql
@Database/Scripts/02_Create_SYS_ROLE_Procedures.sql
@Database/Scripts/03_Create_SYS_CURRENCY_Procedures.sql
@Database/Scripts/04_Create_SYS_BRANCH_Procedures.sql
@Database/Scripts/04_Create_SYS_COMPANY_Procedures.sql
@Database/Scripts/05_Create_SYS_USERS_Procedures.sql
@Database/Scripts/06_Insert_Test_Data.sql
@Database/Scripts/07_Add_RefreshToken_To_Users.sql
```

### Security Notes

- Never commit sensitive data (passwords, connection strings) to Git
- Use `.env` file for configuration
- Keep your `.env` file secure on the server
- Use strong passwords for database and JWT secret
- Consider using SSH keys instead of passwords for server access

### Continuous Deployment

For automatic deployment on every push, consider setting up:
- GitHub Actions
- GitLab CI/CD
- Jenkins
- Other CI/CD tools

See the repository for CI/CD configuration examples.
