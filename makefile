.PHONY: build run help hooks format format-check lint test
.DEFAULT_GOAL := help

ARCH := x64
OS := win
CONFIGURATION := Release
DOTNET := net10.0

help:
	@echo "ARCH=x64|x86|arm64"
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
	dotnet publish -p:publishsinglefile=true -p:includenativelibrariesforselfextract=true --self-contained=true -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj --output src/Zametek.ProjectPlan/bin/$(CONFIGURATION)/$(DOTNET)/$(OS)-$(ARCH)/publish/

publish-cli: build-cli ## publish projectplan.net cli
	dotnet publish -p:publishsinglefile=true -p:includenativelibrariesforselfextract=true --self-contained=true -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj --output src/Zametek.ProjectPlan.CommandLine/bin/$(CONFIGURATION)/$(DOTNET)/$(OS)-$(ARCH)/publish/

publish: publish-desktop publish-cli ## publish projectplan.net and projectplan.net cli


hooks: ## Install pre-commit hooks (run once after cloning)
	dotnet tool restore
	dotnet husky install

format: ## Apply code formatting (style rules only)
	dotnet format style

format-check: ## Check code style without modifying files
	dotnet format style --verify-no-changes

lint: ## Build the solution (NU1903 warnings logged but not errors)
	dotnet build --configuration Release

test: ## Run all tests
	dotnet test --configuration Release
