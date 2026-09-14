FROM node:22-alpine AS build
WORKDIR /src
COPY package*.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY . .
RUN --mount=type=cache,target=/src/.angular/cache npm run build

FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY nginx-proxy-headers.conf /etc/nginx/proxy-headers.conf
COPY --from=build /src/dist/Comments.Frontend/browser /usr/share/nginx/html
EXPOSE 80
