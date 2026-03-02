namespace FC.Engine.Infrastructure.Validation;

/// <summary>
/// Shunting-yard expression parser. Parses arithmetic expressions with variable references.
/// Supports: +, -, *, /, parentheses, function calls (ABS), comparisons (=, !=, >, <, >=, <=).
/// </summary>
public class ExpressionParser
{
    private readonly ExpressionTokenizer _tokenizer = new();

    public ExpressionResult Evaluate(string expression, Dictionary<string, decimal> variables)
    {
        var tokens = _tokenizer.Tokenize(expression);

        // Split on comparison operator: left_expr COMPARISON right_expr
        var compIdx = FindComparisonIndex(tokens);
        if (compIdx >= 0)
        {
            var leftTokens = tokens.Take(compIdx).ToList();
            var comparison = tokens[compIdx].Value;
            var rightTokens = tokens.Skip(compIdx + 1).ToList();

            var leftVal = EvaluateArithmetic(leftTokens, variables);
            var rightVal = EvaluateArithmetic(rightTokens, variables);

            var passes = comparison switch
            {
                "=" => leftVal == rightVal,
                "!=" => leftVal != rightVal,
                ">" => leftVal > rightVal,
                "<" => leftVal < rightVal,
                ">=" => leftVal >= rightVal,
                "<=" => leftVal <= rightVal,
                _ => throw new ArgumentException($"Unknown comparison operator: {comparison}")
            };

            return new ExpressionResult(passes, leftVal, rightVal);
        }

        // No comparison — just evaluate the expression and return the value
        var result = EvaluateArithmetic(tokens, variables);
        return new ExpressionResult(true, result, null);
    }

    private static int FindComparisonIndex(List<Token> tokens)
    {
        var depth = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type == TokenType.LeftParen) depth++;
            else if (tokens[i].Type == TokenType.RightParen) depth--;
            else if (tokens[i].Type == TokenType.Comparison && depth == 0)
                return i;
        }
        return -1;
    }

    private static decimal EvaluateArithmetic(List<Token> tokens, Dictionary<string, decimal> variables)
    {
        var outputQueue = new Queue<Token>();
        var operatorStack = new Stack<Token>();

        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.Variable:
                    outputQueue.Enqueue(token);
                    break;

                case TokenType.Function:
                    operatorStack.Push(token);
                    break;

                case TokenType.Operator:
                    while (operatorStack.Count > 0 &&
                           operatorStack.Peek().Type == TokenType.Operator &&
                           Precedence(operatorStack.Peek().Value) >= Precedence(token.Value))
                    {
                        outputQueue.Enqueue(operatorStack.Pop());
                    }
                    operatorStack.Push(token);
                    break;

                case TokenType.LeftParen:
                    operatorStack.Push(token);
                    break;

                case TokenType.RightParen:
                    while (operatorStack.Count > 0 && operatorStack.Peek().Type != TokenType.LeftParen)
                        outputQueue.Enqueue(operatorStack.Pop());

                    if (operatorStack.Count == 0)
                        throw new ArgumentException("Mismatched parentheses");
                    operatorStack.Pop(); // Remove left paren

                    if (operatorStack.Count > 0 && operatorStack.Peek().Type == TokenType.Function)
                        outputQueue.Enqueue(operatorStack.Pop());
                    break;
            }
        }

        while (operatorStack.Count > 0)
        {
            var op = operatorStack.Pop();
            if (op.Type == TokenType.LeftParen)
                throw new ArgumentException("Mismatched parentheses");
            outputQueue.Enqueue(op);
        }

        // Evaluate RPN
        var evalStack = new Stack<decimal>();
        while (outputQueue.Count > 0)
        {
            var token = outputQueue.Dequeue();

            switch (token.Type)
            {
                case TokenType.Number:
                    evalStack.Push(decimal.Parse(token.Value));
                    break;

                case TokenType.Variable:
                    if (variables.TryGetValue(token.Value, out var val))
                        evalStack.Push(val);
                    else
                        evalStack.Push(0m); // Unknown variables default to 0
                    break;

                case TokenType.Operator:
                    if (evalStack.Count < 2)
                        throw new ArgumentException($"Not enough operands for operator '{token.Value}'");
                    var right = evalStack.Pop();
                    var left = evalStack.Pop();
                    evalStack.Push(token.Value switch
                    {
                        "+" => left + right,
                        "-" => left - right,
                        "*" => left * right,
                        "/" => right != 0 ? left / right : 0m,
                        _ => throw new ArgumentException($"Unknown operator: {token.Value}")
                    });
                    break;

                case TokenType.Function:
                    if (evalStack.Count < 1)
                        throw new ArgumentException($"Not enough operands for function '{token.Value}'");
                    var arg = evalStack.Pop();
                    evalStack.Push(token.Value switch
                    {
                        "ABS" => Math.Abs(arg),
                        _ => throw new ArgumentException($"Unknown function: {token.Value}")
                    });
                    break;
            }
        }

        return evalStack.Count > 0 ? evalStack.Pop() : 0m;
    }

    private static int Precedence(string op) => op switch
    {
        "+" or "-" => 1,
        "*" or "/" => 2,
        _ => 0
    };
}

public record ExpressionResult(bool Passes, decimal LeftValue, decimal? RightValue);
