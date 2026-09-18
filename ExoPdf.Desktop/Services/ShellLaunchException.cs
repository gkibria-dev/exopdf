namespace ExoPdf.Desktop.Services;

/// <summary>Windows could not open a file or Explorer, for example because no PDF viewer is installed.</summary>
public class ShellLaunchException(string message, Exception? innerException = null) : Exception(message, innerException);
