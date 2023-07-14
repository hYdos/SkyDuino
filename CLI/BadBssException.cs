namespace CLI; 

public class BadBssException : Exception{

    public BadBssException() : base("Tried to overwrite block 0 when the bss does not match the uid") {
    }
}