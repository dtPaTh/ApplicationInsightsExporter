FROM otel/opentelemetry-collector-contrib:latest

COPY ./otel_collector_config.yaml /etc/otelcol-contrib/config.yaml 

EXPOSE 4318

