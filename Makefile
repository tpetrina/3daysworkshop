.PHONY: all

dev:
	docker compose --file docker-compose.dev.yml up --build

build:
	$(MAKE) -C src/api build

run:
	docker compose up --build

kind-create:
	kind create cluster --config infra/kind-config.yaml

deploy-app:
	kubectl apply -f manifests

deploy-e2e:
	$(MAKE) -C src/api build
	$(MAKE) -C src/api push-kind
	$(MAKE) deploy-app
	kubectl rollout restart deploy -n workshop-3days api