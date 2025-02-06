using System.Text.Json;
using System.Text.RegularExpressions;


class DialogueCLI
{
    private DialogueParser _parser;
    private List<Conversation> _conversations;

    public DialogueCLI(string conversationsDirectory)
    {
        _parser = new DialogueParser();
        _conversations = _parser.ParseDirectory(conversationsDirectory);
    }

    public void Run()
    {
        Console.WriteLine("Available Conversations:");
        foreach (var conv in _conversations)
        {
            Console.WriteLine($"- {conv.Name}");
        }

        Console.Write("\nEnter the name of the conversation you want to have: ");
        var selectedConversationName = Console.ReadLine();

        var conversation = _conversations.Find(c => c.Name.Equals(selectedConversationName, StringComparison.OrdinalIgnoreCase));

        if (conversation == null)
        {
            Console.WriteLine("Conversation not found.");
            return;
        }

        StartConversation(conversation);
    }

    private void StartConversation(Conversation conversation)
    {
        var currentNode = conversation.GetNode("start");

        while (currentNode != null)
        {
            Console.WriteLine($"\n{currentNode.Text}");

            if (currentNode.Options.Count == 0)
            {
                break;
            }

            for (int i = 0; i < currentNode.Options.Count; i++)
            {
                if (currentNode.Options[i].Condition())
                {
                    Console.WriteLine($"{i + 1}. {currentNode.Options[i].Text}");
                }
            }

            Console.Write("\nSelect an option (number): ");
            if (int.TryParse(Console.ReadLine(), out int selectedIndex) && selectedIndex > 0 && selectedIndex <= currentNode.Options.Count)
            {
                var selectedOption = currentNode.Options[selectedIndex - 1];
                if (selectedOption.Condition())
                {
                    selectedOption.Action();
                    currentNode = conversation.GetNode(selectedOption.NextNode);
                }
                else
                {
                    Console.WriteLine("Option not available.");
                }
            }
            else
            {
                Console.WriteLine("Invalid selection. Please try again.");
            }

            if (currentNode?.Name == "goodbye")
            {
                Console.WriteLine($"\n{currentNode.Text}");
                Console.WriteLine("Conversation ended.");
                break;
            }
        }
    }

    public static void Main(string[] args)
    {
        //Console.Write("Enter the path to the conversations directory: ");
        //var conversationsDirectory = Console.ReadLine();
        var conversationsDirectory = "../../../Conversations";

        if (!Directory.Exists(conversationsDirectory))
        {
            Console.WriteLine("Directory not found.");
            return;
        }

        var cli = new DialogueCLI(conversationsDirectory);
        cli.Run();
    }
}

public class DialogueNode
{
    public string Name { get; set; }
    public string Text { get; set; }
    public List<DialogueOption> Options { get; set; }
    public Func<bool> Condition { get; set; }

    public DialogueNode()
    {
        Options = new List<DialogueOption>();
        Condition = () => true; // Default condition is always true
    }
}

public class DialogueOption
{
    public string Text { get; set; }
    public string NextNode { get; set; }
    public Action Action { get; set; }
    public Func<bool> Condition { get; set; }

    public DialogueOption()
    {
        Condition = () => true; // Default condition is always true
    }
}

public class Conversation
{
    public string Name { get; private set; }
    public Dictionary<string, DialogueNode> Nodes { get; private set; }

    public Conversation(string name)
    {
        Name = name;
        Nodes = new Dictionary<string, DialogueNode>();
    }

    public DialogueNode GetNode(string nodeName)
    {
        return Nodes.TryGetValue(nodeName, out var node) ? node : null;
    }
}
public class DialogueParser
{
    public Dictionary<string, object> GlobalVariables => GlobalState.Instance.Variables;

    public List<Conversation> ParseDirectory(string directoryPath)
    {
        var conversations = new List<Conversation>();
        var files = Directory.GetFiles(directoryPath, "*.txt");

        foreach (var filePath in files)
        {
            var conversation = ParseConversation(filePath);
            conversations.Add(conversation);
        }

        GlobalState.Instance.ValidateVariables();

        return conversations;
    }

    private Conversation ParseConversation(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var conversation = new Conversation(Path.GetFileNameWithoutExtension(filePath));
        DialogueNode currentNode = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("//"))
                continue;

            if (trimmedLine.Contains("?="))
            {
                ParseVariableInitialization(trimmedLine, filePath);
                continue;
            }

            var nodeMatch = Regex.Match(trimmedLine, @"^==(\w+)==$");
            if (nodeMatch.Success)
            {
                currentNode = new DialogueNode { Name = nodeMatch.Groups[1].Value };
                conversation.Nodes[currentNode.Name] = currentNode;
                continue;
            }

            if (currentNode == null) throw new InvalidOperationException("Node is not declared.");

            if (trimmedLine.StartsWith("{}"))
            {
                currentNode.Text = ExtractText(trimmedLine);
                continue;
            }

            if (trimmedLine.StartsWith("=>"))
            {
                var option = ParseOption(trimmedLine);
                currentNode.Options.Add(option);
            }
            else if (trimmedLine.StartsWith("=x"))
            {
                var option = ParseGoodbyeOption(trimmedLine);
                currentNode.Options.Add(option);
            }
        }

        return conversation;
    }

    private void ParseVariableInitialization(string line, string file)
    {
        var parts = line.Split(new[] { "?=" }, StringSplitOptions.None);
        var name = parts[0].Trim();
        var n_clean = parts[1].Remove(parts[1].Length - 1).Trim();
        var value = EvaluateExpression(n_clean);

        GlobalState.Instance.InitializeVariable(name, value, file);
    }

    private DialogueOption ParseOption(string line)
    {
        var condition = ExtractCondition(line);
        var parts = line.Split(new[] { "->" }, StringSplitOptions.None);

        var optionText = ExtractText(parts[0]);
        var nextNode = ExtractNextNode(parts[1]);
        var action = ExtractAction(parts[1]);

        return new DialogueOption
        {
            Text = optionText,
            NextNode = nextNode,
            Action = action,
            Condition = condition
        };
    }

    private DialogueOption ParseGoodbyeOption(string line)
    {
        var match = Regex.Match(line, @"=x\s*\{""([^""]*)""\}");
        if (!match.Success) throw new InvalidOperationException("Invalid =x syntax.");

        var optionText = match.Groups[1].Value;

        return new DialogueOption
        {
            Text = optionText,
            NextNode = "goodbye",
            Action = () => { },  // No action
            Condition = () => true  // Always true
        };
    }

    private string ExtractText(string part)
    {
        var match = Regex.Match(part, @"\{""([^""]*)""\}");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private Func<bool> ExtractCondition(string part)
    {
        var match = Regex.Match(part, @"\{(.*?)\}");
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
            return () => true;

        var condition = match.Groups[1].Value.Trim();
        TrackVariableUsage(condition.Split("=")[0].Trim());
        // Eval to a value or coanstatn before tracking plsss
        // TODO
        return () => EvaluateCondition(condition);
    }

    private string ExtractNextNode(string part)
    {
        var match = Regex.Match(part, @"\s{(.*?)}");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private Action ExtractAction(string part)
    {
        var match = Regex.Match(part, @"\s.*}\s{(.*)}");
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
            return () => { };

        var action = match.Groups[1].Value.Trim();
        return () => EvaluateAction(action);
    }

    private object EvaluateExpression(string expression)
    {
        if (expression.Equals("{}"))
            return null;

        if (int.TryParse(expression, out var intValue))
            return intValue;

        if (expression.StartsWith("\"") && expression.EndsWith("\""))
            return expression.Trim('"');

        if (expression.Equals("true"))
            return true;

        if (expression.Equals("false"))
            return false;

        GlobalState.Instance.UseVariable(expression);

        if (GlobalVariables.ContainsKey(expression))
            return GlobalVariables[expression];

        return null;
    }

    private bool EvaluateAction(string action)
    {
        try
        {
            // Simple condition evaluation (you can expand this for more complex conditions)
            var parts = action.Split(new[] { "+=" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var left = parts[0].Trim();
                var right = EvaluateExpression(parts[1].Trim());

                if (right is int i)
                {
                    var v = GlobalState.Instance.GetVariable(left);

                    if (v is int j)
                    {
                        GlobalState.Instance.SetVariable(left, (i + j));
                    }
                }

                return true;
            }


            parts = action.Split(new[] { "=" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var left = parts[0].Trim();
                var right = EvaluateExpression(parts[1].Trim());
                GlobalState.Instance.SetVariable(left, right);
                return true;
            }

            // Add more actions if needed

            return false;
        }
        catch
        {
            Console.WriteLine($"Error evaluating action: {action}");
            return false;
        }
    }

    private bool EvaluateCondition(string condition)
    {
        try
        {
            // Simple condition evaluation (you can expand this for more complex conditions)
            var parts = condition.Split(new[] { "==" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var left = EvaluateExpression(parts[0].Trim());
                var right = EvaluateExpression(parts[1].Trim());
                return left.Equals(right);
            }

            parts = condition.Split(new[] { "!=" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var left = EvaluateExpression(parts[0].Trim());
                var right = EvaluateExpression(parts[1].Trim());
                return !left.Equals(right);
            }

            // Add more conditions if needed

            return false;
        }
        catch
        {
            Console.WriteLine($"Error evaluating condition: {condition}");
            return false;
        }
    }

    private void TrackVariableUsage(string expression)
    {
        var variables = Regex.Matches(expression, @"\b\w+\b")
                             .Cast<Match>()
                             .Select(m => m.Value)
                             .Distinct();

        foreach (var variable in variables)
        {
            GlobalState.Instance.UseVariable(variable);
        }
    }
}

public class GlobalState
{
    private static GlobalState _instance;
    public Dictionary<string, object> Variables { get; private set; }
    private Dictionary<string, string> _initializedVariables;
    private HashSet<string> _usedVariables;

    private GlobalState()
    {
        Variables = new Dictionary<string, object>();
        _initializedVariables = new Dictionary<string, string>();
        _usedVariables = new HashSet<string>();
    }

    public static GlobalState Instance => _instance ??= new GlobalState();

    public void InitializeVariable(string name, object value, string file)
    {
        if (!_initializedVariables.ContainsKey(name))
        {
            Variables[name] = value;
            _initializedVariables.Add(name, file);
        }
    }

    public void SetVariable(string name, object value)
    {
        Variables[name] = value;
    }

    public object GetVariable(string name)
    {
        if (Variables.TryGetValue(name, out var value))
        {
            _usedVariables.Add(name);
            return value;
        }
        throw new KeyNotFoundException($"Variable '{name}' not found.");
    }

    public void UseVariable(string name)
    {
        if (Variables.ContainsKey(name))
        {
            _usedVariables.Add(name);
        }
        else
        {
            Console.WriteLine($"Warning: Variable '{name}' used but not initialized.");
        }
    }

    public void ValidateVariables()
    {
        foreach (var e in _initializedVariables)
        {
            if (!_usedVariables.Contains(e.Key))
            {
                Console.WriteLine($"Warning: Variable '{e.Key}' initialized but never used. File '{e.Value}'");
            }
        }
    }

    public void SaveState(string filePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(Variables, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
            Console.WriteLine("Global state saved successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving global state: {ex.Message}");
        }
    }

    public void LoadState(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                Variables = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                _initializedVariables = new Dictionary<string, string>();
                Console.WriteLine("Global state loaded successfully.");
            }
            else
            {
                Console.WriteLine("No existing global state found. Starting with a new state.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading global state: {ex.Message}");
        }
    }
}
