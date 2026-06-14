using MyProject12.Models;
using System.Collections.Concurrent;

namespace MyProject12.Services
{
    public enum HarnessRunType
    {
        Yearly = 0,
        Monthly = 1,
        Cancel = 2
    }

    public sealed class PaymentsHarnessRun
    {
        public string RunId { get; set; } = string.Empty;
        public HarnessRunType Type { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Status { get; set; } = "Pending";
        public string Reason { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string CField1 { get; set; } = string.Empty;
        public string ProcessToken { get; set; } = string.Empty;
        public int? ProcessId { get; set; }
        public int? DirectDebitId { get; set; }
        public string TransactionToken { get; set; } = string.Empty;
        public int? TransactionId { get; set; }
        public string Asmachta { get; set; } = string.Empty;
        public DateTime LastUpdatedUtc { get; set; }
    }

    public sealed class PaymentsHarnessEvent
    {
        public DateTime ReceivedUtc { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string LinkedRunId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? StatusCode { get; set; }
        public float? Amount { get; set; }
        public int? PaymentType { get; set; }
        public string Asmachta { get; set; } = string.Empty;
        public string PayerEmail { get; set; } = string.Empty;
        public int? DirectDebitId { get; set; }
        public int? ProcessId { get; set; }
        public string ProcessToken { get; set; } = string.Empty;
        public int? TransactionId { get; set; }
        public string TransactionToken { get; set; } = string.Empty;
    }

    public sealed class PaymentsHarnessRawWebhook
    {
        public DateTime ReceivedUtc { get; set; }
        public string Method { get; set; } = "POST";
        public string Path { get; set; } = string.Empty;
        public string QueryString { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Headers { get; set; } = string.Empty;
        public string CField1 { get; set; } = string.Empty;
        public string LinkedRunId { get; set; } = string.Empty;
        public bool IsHarness { get; set; }
        public string ClassificationReason { get; set; } = string.Empty;
        public string TransactionToken { get; set; } = string.Empty;
        public int? TransactionId { get; set; }
        public int? DirectDebitId { get; set; }
        public int? ProcessId { get; set; }
        public string ProcessToken { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? StatusCode { get; set; }
        public string RawBody { get; set; } = string.Empty;
    }

    public sealed class HarnessClassification
    {
        public bool IsHarness { get; set; }
        public string RunId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class PaymentsHarnessSnapshot
    {
        public List<PaymentsHarnessRun> Runs { get; set; } = new();
        public List<PaymentsHarnessEvent> Events { get; set; } = new();
        public List<PaymentsHarnessEvent> RecurringEvents { get; set; } = new();
        public List<PaymentsHarnessRawWebhook> RawWebhooks { get; set; } = new();
    }

    public sealed class PaymentsHarnessStore
    {
        private readonly object _gate = new();
        private readonly ConcurrentDictionary<string, PaymentsHarnessRun> _runs = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PaymentsHarnessEvent> _events = new();
        private readonly List<PaymentsHarnessRawWebhook> _rawWebhooks = new();
        private readonly Dictionary<string, string> _cFieldToRun = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _processTokenToRun = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _directDebitToRun = new();
        private readonly Dictionary<string, string> _asmachtaToRun = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _transactionTokenToRun = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _seenTransactionTokens = new(StringComparer.OrdinalIgnoreCase);
        private const int MaxEvents = 500;
        private const int MaxRawWebhooks = 1000;

        private static string DetectPayloadFormat(MeshulamWebhookPayload? payload)
        {
            if (payload == null)
            {
                return "none";
            }

            if (payload.IsJson)
            {
                return "json";
            }

            if (payload.HasForm)
            {
                return "form";
            }

            if (string.IsNullOrWhiteSpace(payload.RawBody))
            {
                return "empty";
            }

            return !string.IsNullOrWhiteSpace(payload.ContentType)
                ? $"raw:{payload.ContentType}"
                : "raw";
        }

        public PaymentsHarnessRun CreateRun(HarnessRunType type)
        {
            var run = new PaymentsHarnessRun
            {
                RunId = Guid.NewGuid().ToString("N"),
                Type = type,
                CreatedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _runs[run.RunId] = run;
            return run;
        }

        public void MarkRunCreated(PaymentsHarnessRun run, string checkoutUrl, string cField1)
        {
            lock (_gate)
            {
                run.CheckoutUrl = checkoutUrl ?? string.Empty;
                run.CField1 = cField1 ?? string.Empty;
                run.Status = string.IsNullOrWhiteSpace(checkoutUrl) ? "Failed" : "Awaiting Payment";
                run.LastUpdatedUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(run.CField1))
                {
                    _cFieldToRun[run.CField1] = run.RunId;
                }
            }
        }

        public void MarkRunFailed(PaymentsHarnessRun run, string reason)
        {
            lock (_gate)
            {
                run.Status = "Failed";
                run.Reason = reason ?? string.Empty;
                run.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        public void MarkRunCompleted(PaymentsHarnessRun run, string reason)
        {
            lock (_gate)
            {
                run.Status = "PASS";
                run.Reason = reason ?? string.Empty;
                run.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        public HarnessClassification Classify(MeshulamWebhookPayload payload, Transaction transaction)
        {
            var cField1 = ExtractHarnessCField1(payload);
            if (!string.IsNullOrWhiteSpace(cField1))
            {
                if (_cFieldToRun.TryGetValue(cField1, out var mappedRunId))
                {
                    return new HarnessClassification { IsHarness = true, RunId = mappedRunId, Reason = "cField1" };
                }

                var runId = ExtractRunIdFromHarnessCField(cField1);
                if (!string.IsNullOrWhiteSpace(runId))
                {
                    return new HarnessClassification { IsHarness = true, RunId = runId, Reason = "cField1-unmapped" };
                }
            }

            if (transaction != null)
            {
                if (!string.IsNullOrWhiteSpace(transaction.ProcessToken) &&
                    _processTokenToRun.TryGetValue(transaction.ProcessToken, out var runFromProcessToken))
                {
                    return new HarnessClassification { IsHarness = true, RunId = runFromProcessToken, Reason = "processToken" };
                }

                if (transaction.DirectDebitId.HasValue &&
                    _directDebitToRun.TryGetValue(transaction.DirectDebitId.Value, out var runFromDirectDebit))
                {
                    return new HarnessClassification { IsHarness = true, RunId = runFromDirectDebit, Reason = "directDebitId" };
                }

                if (!string.IsNullOrWhiteSpace(transaction.Asmachta) &&
                    _asmachtaToRun.TryGetValue(transaction.Asmachta, out var runFromAsmachta))
                {
                    return new HarnessClassification { IsHarness = true, RunId = runFromAsmachta, Reason = "asmachta" };
                }

                if (!string.IsNullOrWhiteSpace(transaction.TransactionToken) &&
                    _transactionTokenToRun.TryGetValue(transaction.TransactionToken, out var runFromToken))
                {
                    return new HarnessClassification { IsHarness = true, RunId = runFromToken, Reason = "transactionToken" };
                }
            }

            return new HarnessClassification { IsHarness = false, Reason = "production" };
        }

        public void RecordHarnessEvent(string path, MeshulamWebhookPayload payload, Transaction transaction, HarnessClassification classification, string result, string reason)
        {
            var evt = new PaymentsHarnessEvent
            {
                ReceivedUtc = DateTime.UtcNow,
                Path = path ?? string.Empty,
                Format = DetectPayloadFormat(payload),
                Result = result ?? string.Empty,
                Reason = reason ?? string.Empty,
                LinkedRunId = classification?.RunId ?? string.Empty,
                Status = transaction?.Status ?? string.Empty,
                StatusCode = transaction?.StatusCode,
                Amount = transaction?.Sum,
                PaymentType = transaction?.PaymentType,
                Asmachta = transaction?.Asmachta ?? string.Empty,
                PayerEmail = transaction?.PayerEmail ?? string.Empty,
                DirectDebitId = transaction?.DirectDebitId,
                ProcessId = transaction?.ProcessId,
                ProcessToken = transaction?.ProcessToken ?? string.Empty,
                TransactionId = transaction?.TransactionId,
                TransactionToken = transaction?.TransactionToken ?? string.Empty
            };

            lock (_gate)
            {
                if (!string.IsNullOrWhiteSpace(evt.TransactionToken))
                {
                    _seenTransactionTokens.Add(evt.TransactionToken);
                }

                _events.Insert(0, evt);
                if (_events.Count > MaxEvents)
                {
                    _events.RemoveRange(MaxEvents, _events.Count - MaxEvents);
                }

                if (!string.IsNullOrWhiteSpace(evt.LinkedRunId) && _runs.TryGetValue(evt.LinkedRunId, out var run))
                {
                    UpdateRunCorrelation(run, evt);
                    run.Status = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase) ? "PASS" : "Failed";
                    run.Reason = evt.Reason;
                    run.LastUpdatedUtc = DateTime.UtcNow;
                }
            }
        }

        public void RecordRawWebhook(
            string path,
            MeshulamWebhookPayload payload,
            Transaction? transaction,
            HarnessClassification? classification,
            string? method = null,
            string? queryString = null,
            string? headers = null)
        {
            var raw = new PaymentsHarnessRawWebhook
            {
                ReceivedUtc = DateTime.UtcNow,
                Method = string.IsNullOrWhiteSpace(method) ? "POST" : method.Trim().ToUpperInvariant(),
                Path = path ?? string.Empty,
                QueryString = queryString ?? string.Empty,
                Format = DetectPayloadFormat(payload),
                ContentType = payload?.ContentType ?? string.Empty,
                Headers = headers ?? string.Empty,
                CField1 = ExtractHarnessCField1(payload),
                LinkedRunId = classification?.RunId ?? string.Empty,
                IsHarness = classification?.IsHarness == true,
                ClassificationReason = classification?.Reason ?? string.Empty,
                TransactionToken = transaction?.TransactionToken ?? string.Empty,
                TransactionId = transaction?.TransactionId,
                DirectDebitId = transaction?.DirectDebitId,
                ProcessId = transaction?.ProcessId,
                ProcessToken = transaction?.ProcessToken ?? string.Empty,
                Status = transaction?.Status ?? string.Empty,
                StatusCode = transaction?.StatusCode,
                RawBody = payload?.RawBody ?? string.Empty
            };

            lock (_gate)
            {
                _rawWebhooks.Insert(0, raw);
                if (_rawWebhooks.Count > MaxRawWebhooks)
                {
                    _rawWebhooks.RemoveRange(MaxRawWebhooks, _rawWebhooks.Count - MaxRawWebhooks);
                }
            }
        }

        public bool HasSeenTransactionToken(string? transactionToken)
        {
            if (string.IsNullOrWhiteSpace(transactionToken))
            {
                return false;
            }

            lock (_gate)
            {
                return _seenTransactionTokens.Contains(transactionToken);
            }
        }

        public bool TryGetRun(string runId, out PaymentsHarnessRun? run)
        {
            if (_runs.TryGetValue(runId, out var value))
            {
                run = value;
                return true;
            }

            run = null;
            return false;
        }

        public void UpdateRunFromWebhook(string runId, Transaction? transaction)
        {
            if (string.IsNullOrWhiteSpace(runId) || transaction == null)
            {
                return;
            }

            lock (_gate)
            {
                if (_runs.TryGetValue(runId, out var run))
                {
                    var evt = new PaymentsHarnessEvent
                    {
                        LinkedRunId = runId,
                        ProcessId = transaction.ProcessId,
                        ProcessToken = transaction.ProcessToken ?? string.Empty,
                        DirectDebitId = transaction.DirectDebitId,
                        Asmachta = transaction.Asmachta ?? string.Empty,
                        TransactionToken = transaction.TransactionToken ?? string.Empty
                    };
                    UpdateRunCorrelation(run, evt);
                    run.LastUpdatedUtc = DateTime.UtcNow;
                }
            }
        }

        public PaymentsHarnessSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                var runs = _runs.Values
                    .OrderByDescending(x => x.CreatedUtc)
                    .Take(200)
                    .Select(CloneRun)
                    .ToList();
                var events = _events.Take(300).Select(CloneEvent).ToList();
                var recurring = events
                    .Where(x => x.Path.EndsWith("/meshulam-dd-success", StringComparison.OrdinalIgnoreCase) ||
                                x.Path.EndsWith("/meshulam-dd-failure", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var rawWebhooks = _rawWebhooks.Take(500).Select(CloneRawWebhook).ToList();
                return new PaymentsHarnessSnapshot
                {
                    Runs = runs,
                    Events = events,
                    RecurringEvents = recurring,
                    RawWebhooks = rawWebhooks
                };
            }
        }

        public PaymentsHarnessRun? FindLatestRecurringRun()
        {
            lock (_gate)
            {
                return _runs.Values
                    .Where(x => x.Type == HarnessRunType.Monthly && x.DirectDebitId.HasValue)
                    .OrderByDescending(x => x.LastUpdatedUtc)
                    .Select(CloneRun)
                    .FirstOrDefault();
            }
        }

        public static string BuildHarnessCField(string runId, HarnessRunType type)
        {
            return $"HARNESS_{runId}_{type}";
        }

        public static string ExtractHarnessCField1(MeshulamWebhookPayload payload)
        {
            return payload?.GetValue(
                "data[customFields][cField1]",
                "data[customFields].cField1",
                "data[customFields][CField1]",
                "data[cField1]",
                "customFields[cField1]",
                "customFields.cField1",
                "cField1");
        }

        private void UpdateRunCorrelation(PaymentsHarnessRun run, PaymentsHarnessEvent evt)
        {
            if (!string.IsNullOrWhiteSpace(evt.ProcessToken))
            {
                run.ProcessToken = evt.ProcessToken;
                _processTokenToRun[evt.ProcessToken] = run.RunId;
            }

            if (evt.ProcessId.HasValue)
            {
                run.ProcessId = evt.ProcessId;
            }

            if (evt.DirectDebitId.HasValue)
            {
                run.DirectDebitId = evt.DirectDebitId;
                _directDebitToRun[evt.DirectDebitId.Value] = run.RunId;
            }

            if (!string.IsNullOrWhiteSpace(evt.Asmachta))
            {
                run.Asmachta = evt.Asmachta;
                _asmachtaToRun[evt.Asmachta] = run.RunId;
            }

            if (!string.IsNullOrWhiteSpace(evt.TransactionToken))
            {
                run.TransactionToken = evt.TransactionToken;
                _transactionTokenToRun[evt.TransactionToken] = run.RunId;
            }

            if (evt.TransactionId.HasValue)
            {
                run.TransactionId = evt.TransactionId;
            }
        }

        private static string ExtractRunIdFromHarnessCField(string cField1)
        {
            if (string.IsNullOrWhiteSpace(cField1))
            {
                return string.Empty;
            }

            // Legacy format: HARNESS:{runId}:{type}
            var legacyParts = cField1.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (legacyParts.Length >= 2 && legacyParts[0].Equals("HARNESS", StringComparison.OrdinalIgnoreCase))
            {
                return legacyParts[1];
            }

            // Current format: HARNESS_{runId}_{type}
            var currentParts = cField1.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (currentParts.Length >= 3 && currentParts[0].Equals("HARNESS", StringComparison.OrdinalIgnoreCase))
            {
                return currentParts[1];
            }

            // Plain runId format (current default).
            if (Guid.TryParseExact(cField1, "N", out _))
            {
                return cField1;
            }

            return string.Empty;
        }

        private static PaymentsHarnessRun CloneRun(PaymentsHarnessRun source)
        {
            return new PaymentsHarnessRun
            {
                RunId = source.RunId,
                Type = source.Type,
                CreatedUtc = source.CreatedUtc,
                Status = source.Status,
                Reason = source.Reason,
                CheckoutUrl = source.CheckoutUrl,
                CField1 = source.CField1,
                ProcessToken = source.ProcessToken,
                ProcessId = source.ProcessId,
                DirectDebitId = source.DirectDebitId,
                TransactionToken = source.TransactionToken,
                TransactionId = source.TransactionId,
                Asmachta = source.Asmachta,
                LastUpdatedUtc = source.LastUpdatedUtc
            };
        }

        private static PaymentsHarnessEvent CloneEvent(PaymentsHarnessEvent source)
        {
            return new PaymentsHarnessEvent
            {
                ReceivedUtc = source.ReceivedUtc,
                Path = source.Path,
                Format = source.Format,
                Result = source.Result,
                Reason = source.Reason,
                LinkedRunId = source.LinkedRunId,
                Status = source.Status,
                StatusCode = source.StatusCode,
                Amount = source.Amount,
                PaymentType = source.PaymentType,
                Asmachta = source.Asmachta,
                PayerEmail = source.PayerEmail,
                DirectDebitId = source.DirectDebitId,
                ProcessId = source.ProcessId,
                ProcessToken = source.ProcessToken,
                TransactionId = source.TransactionId,
                TransactionToken = source.TransactionToken
            };
        }

        private static PaymentsHarnessRawWebhook CloneRawWebhook(PaymentsHarnessRawWebhook source)
        {
            return new PaymentsHarnessRawWebhook
            {
                ReceivedUtc = source.ReceivedUtc,
                Method = source.Method,
                Path = source.Path,
                QueryString = source.QueryString,
                Format = source.Format,
                ContentType = source.ContentType,
                Headers = source.Headers,
                CField1 = source.CField1,
                LinkedRunId = source.LinkedRunId,
                IsHarness = source.IsHarness,
                ClassificationReason = source.ClassificationReason,
                TransactionToken = source.TransactionToken,
                TransactionId = source.TransactionId,
                DirectDebitId = source.DirectDebitId,
                ProcessId = source.ProcessId,
                ProcessToken = source.ProcessToken,
                Status = source.Status,
                StatusCode = source.StatusCode,
                RawBody = source.RawBody
            };
        }
    }
}
