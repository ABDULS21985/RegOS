namespace FC.Engine.Infrastructure.Validation;

public enum TokenType
{
    Number,
    Variable,       // A, B, C or field names
    Operator,       // +, -, *, /
    Comparison,     // =, !=, >, <, >=, <=
    LeftParen,
    RightParen,
    Function        // SUM, COUNT, MAX, MIN, AVG, ABS
}

public record Token(TokenType Type, string Value);

public class ExpressionTokenizer
{
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
        { "SUM", "COUNT", "MAX", "MIN", "AVG", "ABS" };

    public List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        var i = 0;
        var expr = expression.Trim();

        while (i < expr.Length)
        {
            if (char.IsWhiteSpace(expr[i]))
            {
                i++;
                continue;
            }

            // Numbers (including decimals)
            if (char.IsDigit(expr[i]) || (expr[i] == '.' && i + 1 < expr.Length && char.IsDigit(expr[i + 1])))
            {
                var start = i;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.'))
                    i++;
                tokens.Add(new Token(TokenType.Number, expr[start..i]));
                continue;
            }

            // Variables or function names (letters, digits, underscores)
            if (char.IsLetter(expr[i]) || expr[i] == '_')
            {
                var start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_'))
                    i++;
                var name = expr[start..i];

                if (Functions.Contains(name))
                    tokens.Add(new Token(TokenType.Function, name.ToUpperInvariant()));
                else
                    tokens.Add(new Token(TokenType.Variable, name));
                continue;
            }

            // Two-character operators
            if (i + 1 < expr.Length)
            {
                var twoChar = expr.Substring(i, 2);
                if (twoChar is ">=" or "<=" or "!=")
                {
                    tokens.Add(new Token(TokenType.Comparison, twoChar));
                    i += 2;
                    continue;
                }
            }

            // Single character tokens
            switch (expr[i])
            {
                case '+' or '-' or '*' or '/':
                    tokens.Add(new Token(TokenType.Operator, expr[i].ToString()));
                    i++;
                    break;
                case '=' or '>' or '<':
                    tokens.Add(new Token(TokenType.Comparison, expr[i].ToString()));
                    i++;
                    break;
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "("));
                    i++;
                    break;
                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")"));
                    i++;
                    break;
                default:
                    throw new ArgumentException($"Unexpected character '{expr[i]}' at position {i} in expression: {expression}");
            }
        }

        return tokens;
    }
}
