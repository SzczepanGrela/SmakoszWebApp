namespace Smakosz.Application.Common.Interfaces;

public interface IBusinessMetrics
{
    void RecordRegistration(string outcome);
    void RecordLogin(string outcome);
    void RecordReviewSubmitted();
    void RecordPhotoUpload(string target);
    void SetModerationQueueDepth(string kind, int depth);
}
