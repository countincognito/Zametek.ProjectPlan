.PHONY: build run help
.DEFAULT_GOAL := run

help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

build: ## Compile all projects in the ProjectPlan solution
	dotnet build

run-linux: build ## Start ProjectPlan in Linux
	dotnet run --os linux --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

run-mac: build ## Start ProjectPlan in macOS
	dotnet run --os osx --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

publish-win: build ## -p:PublishTrimmed=true -p:PublishReadyToRun=true
	dotnet publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained=true -c Release --os win --output publish/win src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj
