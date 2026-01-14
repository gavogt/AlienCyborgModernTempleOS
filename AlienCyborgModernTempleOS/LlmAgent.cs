namespace AlienCyborgModernTempleOS
{
    public sealed class LlmAgent : IAgent
    {
        public string Name { get; }
        private readonly LmStudioChatClient _llm;
        private readonly string _model;
        private readonly string _system;

        public LlmAgent(string name, LmStudioChatClient llm, string model, string systemPrompt)
        {
            Name = name;
            _llm = llm;
            _model = model;
            _system = systemPrompt;
        }
        public async Task<string> RunAsync(string input, CancellationToken ct)
        {
            var response = await _llm.ChatAsync(_model, new[]
            {
                ("system", _system),
                ("user", input)
            }, ct);

            return response;
        }
    }
}
