#!/bin/bash
# Quick deployment - removes old containers and starts fresh
docker rm -f $(docker ps -a -q --filter "name=thinkonerp") 2>/dev/null
docker run -d --name thinkonerp-api --restart unless-stopped -p 5000:8080 --env-file .env.production thinkonerp_thinkonerp-api:latest
sleep 5
docker logs thinkonerp-api --tail 30
echo ""
echo "✓ Deployment complete!"
echo "Test: curl http://localhost:5000/health"
