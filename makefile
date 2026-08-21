.PHONY: help clean build-desktop build-cli build-browser build publish-desktop publish-cli publish-browser run-browser publish hooks workloads format format-check lint test
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
	dotnet build -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) --self-contained=true src/Zametek.ProjectPlan.Desktop/Zametek.ProjectPlan.Desktop.csproj

build-cli: ## Compile all projects for projectplan.net cli
	dotnet build -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) --self-contained=true src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj

build-browser: ## Compile the web app (requires the wasm-tools workload - see the workloads target)
	dotnet build -c $(CONFIGURATION) src/Zametek.ProjectPlan.Browser/Zametek.ProjectPlan.Browser.csproj

build: build-desktop build-cli build-browser ## Compile all projects



publish-desktop: build-desktop ## publish projectplan.net
	dotnet publish -p:publishsinglefile=true --self-contained=true -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) src/Zametek.ProjectPlan.Desktop/Zametek.ProjectPlan.Desktop.csproj --output src/Zametek.ProjectPlan.Desktop/bin/$(CONFIGURATION)/$(DOTNET)/$(OS)-$(ARCH)/publish/

publish-cli: build-cli ## publish projectplan.net cli
	dotnet publish -p:publishsinglefile=true --self-contained=true -c $(CONFIGURATION) --os $(OS) --arch $(ARCH) src/Zametek.ProjectPlan.CommandLine/Zametek.ProjectPlan.CommandLine.csproj --output src/Zametek.ProjectPlan.CommandLine/bin/$(CONFIGURATION)/$(DOTNET)/$(OS)-$(ARCH)/publish/

publish-browser: build-browser ## publish the web app as a static site (AppBundle)
	dotnet publish -c $(CONFIGURATION) src/Zametek.ProjectPlan.Browser/Zametek.ProjectPlan.Browser.csproj

run-browser: ## Serve the web app locally on http://localhost:5210
	dotnet run --project src/Zametek.ProjectPlan.Browser/Zametek.ProjectPlan.Browser.csproj

publish: publish-desktop publish-cli ## publish projectplan.net and projectplan.net cli


hooks: ## Install pre-commit hooks (run once after cloning)
	dotnet tool restore
	dotnet husky install

workloads: ## Install the WebAssembly build toolchain for the browser head (run once after cloning)
	dotnet workload install wasm-tools

format: ## Apply code formatting (style rules only)
	dotnet format style Zametek.ProjectPlan.slnf

format-check: ## Check code style without modifying files
	dotnet format style --verify-no-changes Zametek.ProjectPlan.slnf

lint: ## Build the solution (NU1903 warnings logged but not errors)
	dotnet build --configuration Release Zametek.ProjectPlan.slnf

test: ## Run all tests
	dotnet test --configuration Release Zametek.ProjectPlan.slnf
