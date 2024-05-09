# 3 Days of DevOps in 1 Day: From Local to Production

Requirements:

- kubectl
  - Optionally: kubectx+kubens
- Kubernetes cluster
  - kind
  - Docker desktop
  - Minikube
  - k3s
  - Managed kubernetes

## Windows

choco install -y kind kubernetes-helm

## macOS

brew install kind helm kubectx

## How to use this repository?

The course is divided in 6 parts. Each part has a pdf presentation followed by exercises written in a markdown file.

Additionally, `infra` and `manifests` folders contain some manifests for the infrastructure part that can speed up following the course and can be used as a starting point for following exercises.
