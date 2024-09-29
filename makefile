.PHONY: build run help
.DEFAULT_GOAL := run

help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

build: ## Compile all projects in the ProjectPlan solution
	dotnet build -c Release

clean:
	dotnet clean -c Release

run-linux: build ## Start ProjectPlan in Linux
	dotnet run --os linux -c Release --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

run-mac: build ## Start ProjectPlan in macOS
	dotnet run --os osx -c Release --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

publish-win: build ## -p:PublishTrimmed=true -p:PublishReadyToRun=true
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os win --arch x64 --output publish/win-x64 src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj
	
publish-win-cli: build ## -p:PublishTrimmed=true -p:PublishReadyToRun=true
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os win --arch x64 --output publish/win-x64-cli src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj

publish-linux: build ## -p:PublishTrimmed=true -p:PublishReadyToRun=true
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os linux --arch x64 --output publish/linux-x64 src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

publish-linux-cli: build ## -p:PublishTrimmed=true -p:PublishReadyToRun=true
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os linux --arch x64 --output publish/linux-x64-cli src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj
