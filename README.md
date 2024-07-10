# OTEL Enabler

This service provides metrics to an OTEL (OpenTelemetry) endpoint which then can be monitored using e.g. prometheus, grafana, opensearch or other.

## Metrics

### Example 1

* metric 1
* metric 2
* metric 3

## Prometheus Metrics Scrape Endpoint

Even though this service is meant to be used as an OTEL service, rather than a Prometheus scraping endpoint, the current version provides a scraping endpoint.

<details>
 <summary><code>GET</code> <code><b>/metrics</b></code> <code>(gets all metrics according to prometheus format)</code></summary>

##### Responses

> | http code     | content-type                      | response                                                            |
> |---------------|-----------------------------------|---------------------------------------------------------------------|
> | `200`         | `text/plain;charset=UTF-8`        | \# TYPE queue_blablabla_messages gauge<br />queue_blablabla_messages{otel_scope_name="ServiceBus",count="active"} 0 1720205607277<br />queue_blablabla_messages{otel_scope_name="ServiceBus",count="deadletter"} 6 1720205607277<br />queue_blablabla_messages{otel_scope_name="ServiceBus",count="total"} 6 1720205607277<br />\# TYPE queue_blub_messages gauge<br />queue_blub_messages{otel_scope_name="ServiceBus",count="active"} 0 1720205607277<br />queue_blub_messages{otel_scope_name="ServiceBus",count="deadletter"} 1 1720205607277<br />queue_blub_messages{otel_scope_name="ServiceBus",count="total"} 1 1720205607277<br />\# TYPE queue_test_messages gauge<br />queue_test_messages{otel_scope_name="ServiceBus",count="active"} 6 1720205607277<br />queue_test_messages{otel_scope_name="ServiceBus",count="deadletter"} 0 1720205607277<br />queue_test_messages{otel_scope_name="ServiceBus",count="total"} 6 1720205607277\#EOF  |

##### Example cURL

> ```javascript
>  curl -X GET -H "Content-Type: application/json" http://localhost:8080/metrics
> ```

</details>

## How to run

### Docker

Make a copy of **compose.example.yaml** and rename it to **compose.yaml**. Also, provide the correct environment variables. 

```docker compose up --build```

### Termina/CMD

In order to run this service please make sure all environment variables are set.

```dotnet run```