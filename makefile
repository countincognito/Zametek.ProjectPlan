.PHONY: build run help
.DEFAULT_GOAL := help

help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

build: ## Compile all projects for ProjectPlan
	dotnet build -c Release --self-contained=true src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

build-cli: ## Compile all projects for ProjectPlan CLI
	dotnet build -c Release --self-contained=true src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj

clean: ## Clean the solution
	dotnet clean -c Release

run-win: build ## Start ProjectPlan in Windows
	dotnet run --os win -c Release --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

run-linux: build ## Start ProjectPlan in Linux
	dotnet run --os linux -c Release --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

run-mac: build ## Start ProjectPlan in macOS
	dotnet run --os osx -c Release --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

publish-win: build ## Publish ProjectPlan in Windows for x64
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os win --arch x64 --output publish/win-x64 src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj
	
publish-win-cli: build-cli ## Publish ProjectPlan CLI in Windows for x64 
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os win --arch x64 --output publish/win-x64-cli src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj

publish-linux: build ## Publish ProjectPlan in Linux for x64 
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os linux --arch x64 --output publish/linux-x64 src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

publish-linux-cli: build-cli ## Publish ProjectPlan CLI in Linux for x64 
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os linux --arch x64 --output publish/linux-x64-cli src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj
