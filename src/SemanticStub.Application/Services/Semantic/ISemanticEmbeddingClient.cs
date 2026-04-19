namespace SemanticStub.Application.Services.Semantic;

/// <summary>
/// Provides embeddings from the configured semantic embedding provider.
/// </summary>
public interface ISemanticEmbeddingClient
{
    /// <summary>
    /// Gets embeddings for the supplied inputs.
    /// </summary>
    /// <param name="inputs">The request and candidate texts to embed.</param>
    /// <returns>The returned embedding vectors in request order.</returns>
    Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(IReadOnlyList<string> inputs);
}
