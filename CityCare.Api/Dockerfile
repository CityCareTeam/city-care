FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CityCare.sln ./
COPY CityCare.Api/CityCare.Api.csproj CityCare.Api/
COPY CityCare.Core/CityCare.Core.csproj CityCare.Core/
COPY CityCare.Infrastructure/CityCare.Infrastructure.csproj CityCare.Infrastructure/

RUN dotnet restore CityCare.Api/CityCare.Api.csproj

COPY . .

RUN dotnet publish CityCare.Api/CityCare.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "CityCare.Api.dll"]
