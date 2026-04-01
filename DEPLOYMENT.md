# ThinkOnErp API - Docker Deployment Guide

This guide explains how to deploy the ThinkOnErp API on Ubuntu using Docker.

## Prerequisites

- Ubuntu 20.04 or later
- Minimum 4GB RAM
- 20GB free disk space
- Sudo privileges
- Oracle Database (external or Docker)

## Quick Start

### 1. Install Docker (if not already installed)

```bash
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER
```

Log out and log back in for group changes to take effect.

### 2. Install Docker Compose

```bash
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
docker-compose --version
```

### 3. Configure Environment Variables

```bash
cp .env.example .env
nano .env
```

Update the following variables:
- `ORACLE_CONNECTION_STRING`: Your Oracle database connection string
- `JWT_SECRET_KEY`: A secure secret key (at least 32 characters)
- Other settings as needed

### 4. Deploy Using Script

```bash
chmod +x deploy.sh
./deploy.sh
```

The script will guide you through the deployment process.

## Manual Deployment

### Development Deployment (with Oracle in Docker)

```bash
# Build and start all services including Oracle
docker-compose up -d

# View logs
docker-compose logs -f

# Check status
docker-compose ps
```

### Production Deployment (external Oracle)

```bash
# Build and start API only
docker-compose -f docker-compose.prod.yml up -d

# View logs
docker-compose -f docker-compose.prod.yml logs -f

# Check status
docker-compose -f docker-compose.prod.yml ps
```

## Service URLs

- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger (Development only)
- **Health Check**: http://localhost:5000/health
- **Nginx (if enabled)**: http://localhost:80 or https://localhost:443

## Database Setup

### If using Oracle in Docker:

1. Wait for Oracle to initialize (first startup takes 5-10 minutes)
2. Connect to Oracle:
```bash
docker exec -it thinkonerp-oracle sqlplus sys/OraclePassword123@//localhost:1521/XE as sysdba
```

3. Run database scripts:
```sql
@/docker-entrypoint-initdb.d/startup/01_Create_Sequences.sql
@/docker-entrypoint-initdb.d/startup/02_Create_SYS_ROLE_Procedures.sql
@/docker-entrypoint-initdb.d/startup/03_Create_SYS_CURRENCY_Procedures.sql
@/docker-entrypoint-initdb.d/startup/04_Create_SYS_COMPANY_Procedures.sql
@/docker-entrypoint-initdb.d/startup/04_Create_SYS_BRANCH_Procedures.sql
@/docker-entrypoint-initdb.d/startup/05_Create_SYS_USERS_Procedures.sql
@/docker-entrypoint-initdb.d/startup/06_Insert_Test_Data.sql
```

### If using external Oracle:

Run the SQL scripts manually on your Oracle database in order:
1. `Database/Scripts/01_Create_Sequences.sql`
2. `Database/Scripts/02_Create_SYS_ROLE_Procedures.sql`
3. `Database/Scripts/03_Create_SYS_CURRENCY_Procedures.sql`
4. `Database/Scripts/04_Create_SYS_COMPANY_Procedures.sql`
5. `Database/Scripts/04_Create_SYS_BRANCH_Procedures.sql`
6. `Database/Scripts/05_Create_SYS_USERS_Procedures.sql`
7. `Database/Scripts/06_Insert_Test_Data.sql`

## Testing the Deployment

### 1. Health Check

```bash
curl http://localhost:5000/health
```

### 2. Login Test

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "admin",
    "password": "Admin@123"
  }'
```

### 3. Get Roles (with token)

```bash
TOKEN="your-jwt-token-here"
curl http://localhost:5000/api/roles \
  -H "Authorization: Bearer $TOKEN"
```

## Docker Commands

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f thinkonerp-api

# Last 100 lines
docker-compose logs --tail=100 thinkonerp-api
```

### Restart Services

```bash
# Restart all
docker-compose restart

# Restart specific service
docker-compose restart thinkonerp-api
```

### Stop Services

```bash
# Stop all
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

### Rebuild

```bash
# Rebuild without cache
docker-compose build --no-cache

# Rebuild and restart
docker-compose up -d --build
```

### Execute Commands in Container

```bash
# Access API container shell
docker exec -it thinkonerp-api /bin/bash

# Access Oracle container
docker exec -it thinkonerp-oracle bash
```

## SSL/HTTPS Configuration

### Using Let's Encrypt (Recommended for Production)

1. Install Certbot:
```bash
sudo apt-get update
sudo apt-get install certbot
```

2. Generate certificates:
```bash
sudo certbot certonly --standalone -d your-domain.com
```

3. Copy certificates:
```bash
sudo mkdir -p nginx/ssl
sudo cp /etc/letsencrypt/live/your-domain.com/fullchain.pem nginx/ssl/cert.pem
sudo cp /etc/letsencrypt/live/your-domain.com/privkey.pem nginx/ssl/key.pem
```

4. Update `nginx/nginx.conf` with your domain name

5. Restart Nginx:
```bash
docker-compose restart nginx
```

### Using Self-Signed Certificates (Development)

```bash
mkdir -p nginx/ssl
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout nginx/ssl/key.pem \
  -out nginx/ssl/cert.pem \
  -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"
```

## Monitoring

### Check Container Health

```bash
docker ps
docker inspect thinkonerp-api | grep -A 10 Health
```

### View Resource Usage

```bash
docker stats
```

### Check Disk Usage

```bash
docker system df
```

## Troubleshooting

### API Not Starting

1. Check logs:
```bash
docker-compose logs thinkonerp-api
```

2. Verify environment variables:
```bash
docker-compose config
```

3. Check Oracle connection:
```bash
docker exec -it thinkonerp-api dotnet --version
```

### Oracle Connection Issues

1. Verify Oracle is running:
```bash
docker ps | grep oracle
```

2. Test Oracle connection:
```bash
docker exec -it thinkonerp-oracle sqlplus sys/OraclePassword123@//localhost:1521/XE as sysdba
```

3. Check Oracle logs:
```bash
docker-compose logs oracle-db
```

### Port Already in Use

```bash
# Find process using port 5000
sudo lsof -i :5000

# Kill process
sudo kill -9 <PID>
```

### Permission Issues

```bash
# Fix file permissions
sudo chown -R $USER:$USER .

# Fix Docker socket permissions
sudo chmod 666 /var/run/docker.sock
```

## Backup and Restore

### Backup Oracle Data

```bash
docker exec thinkonerp-oracle sh -c 'exp system/OraclePassword123@XE file=/tmp/backup.dmp full=y'
docker cp thinkonerp-oracle:/tmp/backup.dmp ./backup-$(date +%Y%m%d).dmp
```

### Restore Oracle Data

```bash
docker cp ./backup.dmp thinkonerp-oracle:/tmp/backup.dmp
docker exec thinkonerp-oracle sh -c 'imp system/OraclePassword123@XE file=/tmp/backup.dmp full=y'
```

## Production Recommendations

1. **Use External Oracle Database**: Don't run Oracle in Docker for production
2. **Enable HTTPS**: Use Let's Encrypt or proper SSL certificates
3. **Set Strong JWT Secret**: Use a cryptographically secure random string
4. **Configure Firewall**: Only expose necessary ports
5. **Enable Logging**: Configure centralized logging (ELK, Splunk, etc.)
6. **Set Resource Limits**: Configure Docker resource constraints
7. **Regular Backups**: Automate database backups
8. **Monitoring**: Set up monitoring and alerting (Prometheus, Grafana)
9. **Update Regularly**: Keep Docker images and dependencies updated
10. **Security Scanning**: Scan images for vulnerabilities

## Updating the Application

```bash
# Pull latest code
git pull

# Rebuild and restart
docker-compose down
docker-compose build --no-cache
docker-compose up -d

# Verify
docker-compose ps
curl http://localhost:5000/health
```

## Uninstalling

```bash
# Stop and remove containers
docker-compose down -v

# Remove images
docker rmi $(docker images 'thinkonerp*' -q)

# Remove unused Docker resources
docker system prune -a
```

## Support

For issues and questions:
- Check logs: `docker-compose logs -f`
- Review documentation: `README.md`
- Check database scripts: `Database/README.md`
- Test data information: `Database/TEST_DATA_README.md`
