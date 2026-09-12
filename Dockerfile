FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Comments.Api/Comments.Api.csproj Comments.Api/
COPY Comments.Application/Comments.Application.csproj Comments.Application/
COPY Comments.Domain/Comments.Domain.csproj Comments.Domain/
COPY Comments.Infrastructure/Comments.Infrastructure.csproj Comments.Infrastructure/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore Comments.Api/Comments.Api.csproj
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish Comments.Api/Comments.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data/uploads
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Comments.Api.dll"]
