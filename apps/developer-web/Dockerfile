FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# 授權伺服器的對外網址在建置時寫入前端（瀏覽器導向 /connect/authorize 用）
ARG VITE_AUTH_SERVER_URL
ENV VITE_AUTH_SERVER_URL=$VITE_AUTH_SERVER_URL
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
