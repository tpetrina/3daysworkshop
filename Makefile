.PHONY: all

dev:
	docker compose --file docker-compose.dev.yml up --build

build:
	$(MAKE) -C src/api build

run:
	docker compose up --build

kind-create:
	kind create cluster --config infra/kind-config.yaml

infra-db:
	kubectl apply -f infra/postgresql-pvc.yaml
	kubectl create namespace db || true
	helm upgrade db bitnami/postgresql \
		--install \
		--namespace db \
		--values infra/postgresql-values.yaml

infra-rabbitmq:
	helm upgrade --install rabbitmq bitnami/rabbitmq \
		--set auth.password=RabbitMqPassword \
		--set auth.erlangCookie=M8pbajEbtndUplgF

deploy-app:
	kubectl apply -f manifests

deploy-e2e:
	$(MAKE) -C src/api build
	$(MAKE) -C src/api push-kind
	$(MAKE) deploy-app
	kubectl rollout restart deploy -n workshop-3days api