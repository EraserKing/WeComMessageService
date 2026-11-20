namespace TeamsPresence.Exceptions
{
    public class TeamsPresenceApiException : Exception
    {
        public int StatusCode { get; }

        public TeamsPresenceApiException(string message, int statusCode, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
