namespace FMsg
{
    public class InvalidFmsgAddressException : Exception
    {
        public InvalidFmsgAddressException(string reason) : base(reason)
        {
        }
    }

    public class FMsgAddress
    {
        public string User { get; private set; }
        public string Domain { get; private set; }

        public FMsgAddress(string user, string domain)
        {
            User = user;
            Domain = domain;
        }

        public override string ToString()
        {
            return "@" + User + "@" + Domain;
        }

        public static FMsgAddress Parse(string address)
        {
            if (address[0] != '@')
                throw new InvalidFmsgAddressException("missing leading @");
            var i = address.IndexOf('@', 1);
            if (i < 1)
                throw new InvalidFmsgAddressException("missing 2nd @");
            var recipient = address.Substring(1, i-1);
            // TODO only letters, numbers _ or hyphen non consecutivly and not at beginning or end
            var domain = address.Substring(i + 1);
            if (Uri.CheckHostName(domain) == UriHostNameType.Unknown)
                throw new InvalidFmsgAddressException($"invalid DNS name: {domain}");

            return new FMsgAddress(recipient, domain);
        }
    }

}
