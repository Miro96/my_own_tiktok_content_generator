using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace TiktokGeneratorConsole;

public class AzureOpenAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deploymentName;
    private readonly ChatClient _chatClient;

    public AzureOpenAIService(IConfiguration configuration)
    {
        var apiKey = configuration["AzureSettings:OpenAI:ApiKey"];
        var endpoint = new Uri(configuration["AzureSettings:OpenAI:ApiEndpoint"]);
        _deploymentName = configuration["AzureSettings:OpenAI:DeploymentName-Text"];

        // Инициализация клиента OpenAI
        _client = new AzureOpenAIClient(endpoint,  new AzureKeyCredential(apiKey));
        _chatClient = _client.GetChatClient(_deploymentName);
    }

    public async Task<string> GenerateChatScriptAsync(string prompt, string systemPrompt)
    {
        try
        {
            // Создание списка сообщений для чата
            var chatMessages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(prompt)
            };

            // Настройка параметров для chat completions
            var chatOptions = new ChatCompletionOptions()
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 2000
            };

            // Вызов метода для получения ответа
            var chatCompletionResponse = await _chatClient.CompleteChatAsync(chatMessages, chatOptions);

            // Возвращаем первый ответ
            return chatCompletionResponse.Value.Content[0].Text;
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"Error API: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Undefined error: {ex.Message}");
            throw;
        }
    }
}