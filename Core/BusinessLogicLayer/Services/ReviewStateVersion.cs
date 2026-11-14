using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataAccessLayer.Entities;
namespace BusinessLogicLayer.Services;
public static class ReviewStateVersion {
    public static string Session(ExamSession value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        value.Id, value.IsFinished, value.EvidenceStatus, value.EvidenceRevision, value.EvidenceManifestJson, value.IntegrityHistoryJson,
        value.IntegrityVerdict, Observations = value.Violations.OrderBy(v => v.Id).Select(Observation).ToArray()
    }))));
    public static string Observation(ViolationAlert value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        value.Id, value.Metadata, value.EvidenceUrl, value.ReviewDecision, value.ReviewHistoryJson, value.ReviewedAt
    }))));
}
