using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npc.Api.Dtos;
using Npc.Api.Infrastructure.Metrics;

namespace Npc.Api.Services.Impl
{
    public class TtsService(HttpClient client, IOptions<TtsOptions> ttsOpt, TtsMetrics ttsMetrics, ILogger<TtsService> logger) : ITtsService
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
        private static readonly string[] ValidAudio = ["wav", "mp3", "ogg"];
        public async Task<(Stream Stream, string ContentType, string? FileName)> GenerateVoice(TtsSynthesizeRequest req, CancellationToken ct)
        {   
            
            var sw = Stopwatch.StartNew();
            var succes = false;
            string format = ValidAudio.Contains(req.Format?.ToLower()) ? req.Format!.ToLower() : "wav";
            try
            {
                EnsureTtsConfigured();
                using var content = new StringContent(JsonSerializer.Serialize(new
                {
                    text = req.Text,
                    voice = req.Voice,
                    format = req.Format,
                    sample_rate = req.Sample_Rate,
                    length_scale = req.Length_Scale,
                    noise_scale = req.Noise_Scale,
                    noise_w = req.Noise_W,
                    speaker = req.Speaker
                }, JsonOpts), Encoding.UTF8, "application/json");

                using var resp = await client.PostAsync("synthesize", content, ct);

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new TtsException("Unauthorized (check API key)", 401);


                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new TtsException("Voice not found", 404);

                if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var msg = await resp.Content.ReadAsStringAsync(ct);
                    throw new TtsException($"Bad request: {msg}", 400);
                }

                if (!resp.IsSuccessStatusCode)
                    throw new TtsException($"Upstream error {resp.StatusCode}", (int)resp.StatusCode);

                
                var stream = await resp.Content.ReadAsStreamAsync(ct);
                var contentType = resp.Content.Headers.ContentType?.ToString() ?? "audio/wav";
                var fileName = resp.Content.Headers.ContentDisposition?.FileNameStar
                            ?? resp.Content.Headers.ContentDisposition?.FileName;

                
                return (stream, contentType, fileName);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                succes = false;
                logger.LogDebug("TTS request cancalled");
                throw;
            }

            catch (Exception e)
            {
                succes = false;
                logger.LogError(e, "TTS request exception");
                throw;
            }
            finally
            {
                sw.Stop();
                ttsMetrics.Record(succes, sw.Elapsed.Milliseconds);
            }
        }

        public async Task<string> GetVoices(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var succes = false;

            try
            {
                EnsureTtsConfigured();
                using var resp = await client.GetAsync("voices", ct);
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new TtsException("Unauthorized (check API key)", 401);
                if (!resp.IsSuccessStatusCode)
                    throw new TtsException($"Upstream error {resp.StatusCode}", (int)resp.StatusCode);

    
                succes = true;
                return await resp.Content.ReadAsStringAsync(ct);


            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("TTS request cancalled");
                throw;
            }
            
            catch (Exception e)
            {
                logger.LogError(e, "TTS request exception");
                throw;
            }
            finally
            {
                sw.Stop();
                ttsMetrics.Record(succes, sw.Elapsed.Milliseconds);
            }
        }

        private void EnsureTtsConfigured()
        {
            if (string.IsNullOrWhiteSpace(ttsOpt.Value.BaseUrl))
                throw new InvalidOperationException("TTS not configured (Tts:BaseUrl missing).");
        }
    }


    public class TtsException : Exception
    {
        public int StatusCode { get; }
        public TtsException(string message, int statusCode) : base(message) => StatusCode = statusCode;
    }
}