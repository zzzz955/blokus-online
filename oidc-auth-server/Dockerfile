# Multi-stage build for production optimization
FROM node:18-alpine AS base

# Install dependencies only when needed
FROM base AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --only=production && npm cache clean --force

# Development image
FROM base AS dev
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
CMD ["npm", "run", "dev"]

# Production image
FROM base AS prod
WORKDIR /app

# Create non-root user
RUN addgroup -g 1001 -S nodejs && \
    adduser -S oidcserver -u 1001

# Copy dependencies
COPY --from=deps --chown=oidcserver:nodejs /app/node_modules ./node_modules

# Copy application code
COPY --chown=oidcserver:nodejs . .

# Create necessary directories
RUN mkdir -p logs keys && \
    chown -R oidcserver:nodejs logs keys

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD node -e "const http=require('http');const options={hostname:'localhost',port:process.env.PORT||9000,path:'/health',timeout:10000};const req=http.request(options,(res)=>{process.exit(res.statusCode===200?0:1)});req.on('error',()=>{process.exit(1)});req.end();"

# Switch to non-root user
USER oidcserver

# Expose port
EXPOSE 9000

# Start server
CMD ["npm", "start"]