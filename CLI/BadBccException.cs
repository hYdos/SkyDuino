namespace CLI; 

public class BadBccException : Exception{

    public BadBccException() : base("Tried to overwrite block 0 when the bcc does not match the uid") {
    }
}