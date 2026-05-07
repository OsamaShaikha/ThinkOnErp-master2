#!/bin/bash

echo "🔧 Stopping containers..."
docker-compose down

echo "🗑️  Removing old image..."
docker rmi thinkonerp-thinkonerp-api 2>/dev/null || true

echo "🔨 Rebuilding image..."
docker-compose build --no-cache thinkonerp-api

echo "🚀 Starting application..."
docker-compose up -d thinkonerp-api

echo "⏳ Waiting 5 seconds for startup..."
sleep 5

echo "📋 Showing logs (Ctrl+C to exit)..."
docker logs -f thinkonerp-api
