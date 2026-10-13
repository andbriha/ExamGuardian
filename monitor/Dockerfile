# Stage 1: Build the Angular application
FROM node:20-alpine AS build
WORKDIR /app

# Copy package.json and package-lock.json to leverage Docker caching
COPY package*.json ./

# Use npm ci for faster, more reliable, and reproducible dependency installation
RUN npm ci

# Copy the rest of the application source code
COPY . .

# Declare the build argument passed from docker-compose.yml
# This default value acts as a fallback for local debugging
ARG BACKEND_URL=http://localhost:5111/api

# Inject the dynamic API URL into the Angular environment configuration file
RUN sed -i "s|apiUrl: '.*'|apiUrl: '${BACKEND_URL}'|g" ./src/environments/environment.ts

# Build the Angular application for production
RUN npm run build -- --configuration production

# Stage 2: Serve with Nginx
FROM nginx:alpine

# Copy the compiled Angular artifacts from the build stage
# IMPORTANT: Adjust the path if your Angular version outputs to a different folder (e.g., dist/browser)
COPY --from=build /app/dist/monitor/browser /usr/share/nginx/html

# Copy the custom Nginx configuration to handle Angular routing
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 80 for web traffic
EXPOSE 80

# Start Nginx in the foreground
CMD ["nginx", "-g", "daemon off;"]