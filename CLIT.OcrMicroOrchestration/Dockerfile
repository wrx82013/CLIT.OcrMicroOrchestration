#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["CleanIt.OcrMicroOrchestration/CleanIt.OcrMicroOrchestration.csproj", "CleanIt.OcrMicroOrchestration/"]
COPY ["CleanIt.OcrMicroOrchestration.Infrastructure/CleanIt.OcrMicroOrchestration.Infrastructure.csproj", "CleanIt.OcrMicroOrchestration.Infrastructure/"]
COPY ["CleanIt.OcrMicroOrchestration.Domain/CleanIt.OcrMicroOrchestration.Domain.csproj", "CleanIt.OcrMicroOrchestration.Domain/"]
RUN dotnet restore "CleanIt.OcrMicroOrchestration/CleanIt.OcrMicroOrchestration.csproj"
COPY . .
WORKDIR "/src/CleanIt.OcrMicroOrchestration"
RUN dotnet build "CleanIt.OcrMicroOrchestration.csproj" -c Debug -o /app/build

FROM build AS publish
RUN dotnet publish "CleanIt.OcrMicroOrchestration.csproj" -c Debug -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CleanIt.OcrMicroOrchestration.dll"]