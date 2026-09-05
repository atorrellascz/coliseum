{{- define "coliseum.name" -}}
{{- .Chart.Name -}}
{{- end -}}

{{- define "coliseum.fullname" -}}
{{- printf "%s-%s" .Release.Name .Chart.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "coliseum.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version }}
app.kubernetes.io/name: {{ include "coliseum.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{/* Selector labels for one component: api | worker | mcp | redis */}}
{{- define "coliseum.selectorLabels" -}}
app.kubernetes.io/name: {{ include "coliseum.name" .root }}
app.kubernetes.io/instance: {{ .root.Release.Name }}
app.kubernetes.io/component: {{ .component }}
{{- end -}}

{{- define "coliseum.image" -}}
{{- $registry := .root.Values.image.registry -}}
{{- if $registry -}}{{ $registry }}/{{ .name }}:{{ .root.Values.image.tag }}{{- else -}}{{ .name }}:{{ .root.Values.image.tag }}{{- end -}}
{{- end -}}

{{- define "coliseum.secretName" -}}
{{- default (printf "%s-secrets" (include "coliseum.fullname" .)) .Values.secrets.existingSecret -}}
{{- end -}}

{{- define "coliseum.redisUrl" -}}
{{- if .Values.redis.embedded -}}{{ include "coliseum.fullname" . }}-redis:6379{{- else -}}{{ required "redis.external.url is required when redis.embedded=false and redis.external.fromSecret=false" .Values.redis.external.url }}{{- end -}}
{{- end -}}

{{/* Environment shared by every .NET host. The Redis URL is either computed (embedded), given (external.url) or
     read from the app secret's redisUrl key (external.fromSecret, what Terraform + External Secrets produce). */}}
{{- define "coliseum.commonEnv" -}}
- name: REDIS_URL
{{- if and (not .Values.redis.embedded) .Values.redis.external.fromSecret }}
  valueFrom: { secretKeyRef: { name: {{ include "coliseum.secretName" . }}, key: redisUrl } }
{{- else }}
  value: {{ include "coliseum.redisUrl" . | quote }}
{{- end }}
- name: ASPNETCORE_ENVIRONMENT
  value: Production
{{- $otel := .Values.otel.endpoint }}
{{- if and (not $otel) .Values.monitoring.otelLgtm.enabled }}{{- $otel = printf "http://%s-otel-lgtm:4317" (include "coliseum.fullname" .) }}{{- end }}
{{- if $otel }}
- name: OTEL_EXPORTER_OTLP_ENDPOINT
  value: {{ $otel | quote }}
- name: OTEL_EXPORTER_OTLP_PROTOCOL
  value: grpc
{{- end }}
{{- end -}}

{{- define "coliseum.probes" -}}
livenessProbe:
  httpGet: { path: /healthz/live, port: http }
  initialDelaySeconds: 5
  periodSeconds: 10
readinessProbe:
  httpGet: { path: /healthz/ready, port: http }
  initialDelaySeconds: 3
  periodSeconds: 5
startupProbe:
  httpGet: { path: /healthz/live, port: http }
  failureThreshold: 60
  periodSeconds: 2   # up to 120 s: a cold node pulling other images can slow the first start well past 30 s
{{- end -}}
