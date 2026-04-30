# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY ["HNG_Stage_1/HNG_Stage_1.csproj", "HNG_Stage_1/"]
RUN dotnet restore "HNG_Stage_1/HNG_Stage_1.csproj"

# copy everything else and publish
COPY . .
WORKDIR /src/HNG_Stage_1
RUN dotnet publish "HNG_Stage_1.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Configure ports and environment
# Railway will automatically set the PORT environment variable
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "HNG_Stage_1.dll"]
