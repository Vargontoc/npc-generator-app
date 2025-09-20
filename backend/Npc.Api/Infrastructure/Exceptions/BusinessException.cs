namespace Npc.Api.Infrastructure.Exceptions
{
    public abstract class BusinessException : Exception
    {
        protected BusinessException(string message) : base(message) { }
        protected BusinessException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class EntityNotFoundException : BusinessException
    {
        public EntityNotFoundException(string entityType, Guid id)
            : base($"{entityType} with ID {id} not found") { }

        public EntityNotFoundException(string message) : base(message) { }
    }

    public class ValidationException : BusinessException
    {
        public ValidationException(string message) : base(message) { }

        public ValidationException(string field, string message)
            : base($"Validation failed for {field}: {message}") { }
    }

    public class ExternalServiceException : BusinessException
    {
        public string ServiceName { get; }

        public ExternalServiceException(string serviceName, string message)
            : base($"Error from {serviceName}: {message}")
        {
            ServiceName = serviceName;
        }

        public ExternalServiceException(string serviceName, string message, Exception innerException)
            : base($"Error from {serviceName}: {message}", innerException)
        {
            ServiceName = serviceName;
        }
    }

    public class ModerationException : BusinessException
    {
        public string ViolationType { get; }

        public ModerationException(string violationType, string message)
            : base($"Content moderation failed: {message}")
        {
            ViolationType = violationType;
        }
    }

    public class RateLimitException : BusinessException
    {
        public TimeSpan RetryAfter { get; }

        public RateLimitException(TimeSpan retryAfter)
            : base("Rate limit exceeded. Please try again later.")
        {
            RetryAfter = retryAfter;
        }
    }
}