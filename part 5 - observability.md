# Part 5 - Observability

There are many ways to observe your app. Most cloud providers have a managed
offering. There are also off the shelf solutions in this space. We will use
Prometheus to quickly set up metric scraping and Grafana for visualization.

## 5.1 Install Prometheus

We will install Prometheus via Helm. First we need to add repository:

```sh
helm repo add prometheus https://prometheus-community.github.io/helm-charts
```

Create a file `infra/prometheus.yaml` and paste the content below into it:

<details>
    <summary>infra/prometheus.yaml</summary>

```yaml
# https://github.com/prometheus-community/helm-charts/blob/main/charts/kube-prometheus-stack/values.yaml

defaultRules:
  create: true
  rules:
    etcd: false
    kubeScheduler: false

kubeScheduler:
  enabled: false
kubeEtcd:
  enabled: false

alertmanager:
  enabled: true
  config:
    global:
      resolve_timeout: 1m
      slack_api_url: "SLACK_URL"
    route:
      receiver: "slack-notifications"
      # repeat_interval: 12h
      routes:
        - receiver: "slack-notifications"
          matchers:
            - alertname="ApiDown"
          continue: false
    receivers:
      - name: "slack-notifications"
        slack_configs:
          - channel: "#spam"
            send_resolved: true
            title: "{{ range .Alerts }}{{ .Annotations.summary }}\n{{ end }}"
            text: "{{ range .Alerts }}{{ .Annotations.description }}\n{{ end }}"

additionalPrometheusRulesMap:
  rule-name:
    groups:
      - name: api-down
        rules:
          - alert: ApiDown
            expr: sum(kube_pod_owner{namespace="workshop-3days"}) by (namespace) < 1
            for: 30s
            labels:
              severity: "critical"
              alert_type: "infrastructure"
            annotations:
              description: " The Number of pods from the namespace {{ $labels.namespace }} is lower than the expected 1. "
              summary: "Pod in {{ $labels.namespace }} namespace down"

## Using default values from https://github.com/grafana/helm-charts/blob/main/charts/grafana/values.yaml
##
grafana:
  enabled: true

  ingress:
    enabled: true
    ingressClassName: nginx
    hosts:
      - kind.grafana
  persistence:
    enabled: true
    accessModes: ["ReadWriteOnce"]
    size: 5Gi

prometheusOperator:
  enabled: true

prometheus:
  enabled: true
  additionalServiceMonitors:
    - name: "api"
      selector:
        matchExpressions:
          - key: app
            operator: In
            values:
              - api
      namespaceSelector:
        matchNames:
          - "workshop-3days"
      endpoints:
        - interval: 15s
          port: web
          path: /metrics

  # Persistence between deployments
  storageSpec:
    volumeClaimTemplate:
      spec:
        accessModes: ["ReadWriteOnce"]
        resources:
          requests:
            storage: 5Gi
```

</details>

We are now ready to install it with Helm:

```sh
# Create namespace if it doesn't exist
kubectl create namespace prometheus || true
helm upgrade --install prometheus prometheus/kube-prometheus-stack \
    --namespace prometheus \
    --values infra/prometheus.yaml
```

This might take a while, we can check status by writing:

```sh
kubectl --namespace prometheus get pods -l "release=prometheus"
```

Once it is done, we can check the UI:

```sh
kubectl port-forward -n prometheus svc/prometheus-kube-prometheus-prometheus 9090:9090
```

Open http://localhost:9090 and go to Status > Targets. Notice that we have an error in our api.
That is because we are not exposing any metrics so far!

Grafana is available on http://kind.grafana. Username is `admin`, password `prom-operator`.

## 5.2 Adding metrics to our app

We want the following two packages added to our app `api`:

```sh
dotnet add package prometheus-net.AspNetCore
dotnet add package prometheus-net.AspNetCore.HealthChecks
```

There are different metrics we can use with Prometheus. Built-in ones will give
information about pod, environment and http statistics.

After installing the above package, forward healthchecks to Prometheus by chaining
`.ForwardToPrometheus()` to the healthchecks configuration.

Map the `/metrics` endpoint to expose Prometheus metrics.

```csharp
app.MapMetrics("/metrics")
```

Navigate to http://localhost:PORT/metrics and check what we get by default.

Build and deploy the app with `deploy-e2e` and observe Prometheus slowly picking
up metrics.

# 5.3 Custom metrics

Let's count background jobs and message processing.

Create a new file `PrometheusMetrics.cs` and create a static class `PrometheusMetrics`.

```csharp
static class PrometheusMetrics
{
}
```

Since we will count how many messages are processed, we will use counter metric.
Metric has a name, helpful text, and optional labels.

```csharp
   public static readonly Counter ForecastProcessing = Metrics
        .CreateCounter(
            name: "api_forecast_processing",
            help: "Number of forecasts processed");
```

We can now use it whenever we need to increment the counter:

```csharp
PrometheusMetrics.ForecastProcessing.Inc();
```

Similarly create a counter for message handling.
