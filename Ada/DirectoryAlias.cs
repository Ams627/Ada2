namespace Ada
{
    class DirectoryAlias
    {
        public string Alias { get; set; }
        public string Directory { get; set; }
        public override string ToString()
        {
            return $"alias {Alias}='cd {Directory}'";
        }
    }
}
