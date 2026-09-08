FROM node:22-alpine AS frontend
WORKDIR /src
COPY Comments.Frontend/package*.json ./
RUN npm ci
COPY Comments.Frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Comments.Api/Comments.Api.csproj
RUN dotnet publish Comments.Api/Comments.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=frontend /src/dist/Comments.Frontend/browser ./wwwroot
RUN mkdir -p /data/uploads
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Comments.Api.dll"]
