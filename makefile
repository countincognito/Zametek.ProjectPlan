.PHONY: build run help
.DEFAULT_GOAL := run

help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

build: ## Compile all projects in the ProjectPlan solution
	dotnet build

run: build ## Start ProjectPlan
	dotnet run --project src/Zametek.ProjectPlan/Zametek.ProjectPlan.csproj
