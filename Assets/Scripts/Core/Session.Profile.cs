using System.Text;

namespace LastTide.Core
{
    public sealed partial class Session
    {
        public const int NameMax = 16;

        /// <summary>What the castaway is called on the HUD ("Castaway" until the player picks a name).</summary>
        public string DisplayName { get { return string.IsNullOrEmpty(State.name) ? "Castaway" : State.name; } }

        /// <summary>Tidies a typed name: letters, digits, spaces and ' - . only, single spaces, at most
        /// <see cref="NameMax"/> characters. Returns "" when nothing usable is left.</summary>
        public static string CleanName(string typed)
        {
            if (typed == null) return "";
            var sb = new StringBuilder();
            bool space = false;
            foreach (char c in typed.Trim())
            {
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                if (!(char.IsLetterOrDigit(c) || c == '\'' || c == '-' || c == '.')) continue;
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
                if (sb.Length >= NameMax) break;
            }
            return sb.ToString().Trim();
        }

        /// <summary>Renames the castaway. Returns null when done, else why not.</summary>
        public string Rename(string typed)
        {
            var name = CleanName(typed);
            if (name.Length < 2) return "Names need at least 2 letters";
            State.name = name;
            Changed?.Invoke();
            return null;
        }
    }
}
