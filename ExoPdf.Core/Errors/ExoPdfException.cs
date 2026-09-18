namespace ExoPdf.Core.Errors;

/// <summary>
/// An expected failure with a message fit to show to the user: a missing folder,
/// an unreadable PDF, a folder that cannot be written to. Frontends show
/// <see cref="Exception.Message"/> for these; anything else is a bug.
/// </summary>
public class ExoPdfException(string message, Exception? innerException = null) : Exception(message, innerException);
