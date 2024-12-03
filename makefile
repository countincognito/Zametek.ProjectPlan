.PHONY: build run help
.DEFAULT_GOAL := help

ARCH := x64
OS := win
CONFIGURATION := Release

help:
	@echo "ARCH=x64|x86"
	@echo "OS=win|linux|osx"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'



clean: ## Clean the solution
	dotnet clean -c $(CONFIGURATION)



build-desktop: ## Compile all projects for projectplan.net
	dotnet build -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) --self-contained=true src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj

build-cli: ## Compile all projects for projectplan.net cli
	dotnet build -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) --self-contained=true src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj

build: build-desktop build-cli ## Compile all projects



publish-desktop: build-desktop ## publish projectplan.net
	dotnet publish -p:publishsinglefile=true -p:includenativelibrariesforselfextract=true --self-contained=true -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj --output src/Zametek.ProjectPlan/bin/$(ARCH)/$(CONFIGURATION)/net8.0/$(OS)-$(ARCH)/publish/

publish-cli: build-cli ## publish projectplan.net cli
	dotnet publish -p:publishsinglefile=true -p:includenativelibrariesforselfextract=true --self-contained=true -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj --output src/Zametek.ProjectPlan.CommandLine/bin/$(ARCH)/$(CONFIGURATION)/net8.0/$(OS)-$(ARCH)/publish/

publish: publish-desktop publish-cli ## publish projectplan.net and projectplan.net cli
