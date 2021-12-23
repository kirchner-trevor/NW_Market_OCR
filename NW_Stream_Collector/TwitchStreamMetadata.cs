using NW_Market_Model;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NW_Stream_Collector
{
    public class TwitchStreamMetadata
    {
        private readonly Dictionary<string, string> serverNamesLowercase;
        private readonly Regex wordRegex = new Regex(@"\w+");

        public TwitchStreamMetadata(ConfigurationDatabase configurationDatabase)
        {
            serverNamesLowercase = configurationDatabase.Content.ServerList.Where(_ => _.Name != "Ophir").ToDictionary(_ => _.Name?.ToLowerInvariant(), _ => _.Id);
        }

        //public bool TryExtractServer(string text, out string server)
        //{
        //    server = null;

        //    if (text == null)
        //    {
        //        return false;
        //    }

        //    MatchCollection wordMatches = wordRegex.Matches(text);
        //    string[] textParts = wordMatches.Select(_ => _.Value.ToLowerInvariant()).ToArray();

        //    foreach (string serverName in serverNamesLowercase.Keys)
        //    {
        //        if (textParts.Any(_ => _ == serverName))
        //        {
        //            server = serverNamesLowercase[serverName];
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        public bool TryExtractServer(string text, out string server)
        {
            server = null;

            if (text == null)
            {
                return false;
            }

            MatchCollection wordMatches = wordRegex.Matches(text);
            string[] textParts = wordMatches.Select(_ => _.Value.ToLowerInvariant()).ToArray();

            bool foundMatch = false;
            foreach (string serverName in serverNamesLowercase.Keys)
            {
                string[] serverNameParts = serverName.Split(" ");

                for (int i = 0; i < textParts.Length - serverNameParts.Length + 1; i++)
                {
                    for (int j = 0; j < serverNameParts.Length; j++)
                    {
                        if (textParts[i + j] == serverNameParts[j])
                        {
                            foundMatch = true;
                        }
                        else
                        {
                            foundMatch = false;
                            break;
                        }
                    }

                    if (foundMatch)
                    {
                        break;
                    }
                }

                if (foundMatch)
                {
                    server = serverNamesLowercase[serverName];
                    break;
                }
            }

            return foundMatch;
        }
    }
}
