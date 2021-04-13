using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using ValheimMP.Framework.Events;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Framework
{
    public class ChatCommandManager
    {
        public class CommandInfo
        {
            internal object m_methodObj;
            internal MethodBase m_method;
            internal ChatCommandAttribute m_command;
            public ChatCommandAttribute Command { get { return m_command; } }
        };

        internal List<CommandInfo> m_commands;

        public static ChatCommandManager Instance { get; private set; }

        public static string CommandToken { get; } = "/";

        private ValheimMP m_valheimMP;

        public ChatCommandManager(ValheimMP valheimMP)
        {
            Instance = this;

            m_commands = new List<CommandInfo>();
            m_valheimMP = valheimMP;
            m_valheimMP.OnChatMessage += OnChatMessage;
        }

        public static CommandExecutionLocation GetCurrentExecutionLocation()
        {
            return ValheimMP.IsDedicated ? CommandExecutionLocation.Server : CommandExecutionLocation.Client;
        }

        public void RegisterAll(object obj)
        {
            var methods = obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var execLoc = GetCurrentExecutionLocation();
            for (int i = 0; i < methods.Length; i++)
            {
                var command = methods[i].GetCustomAttribute<ChatCommandAttribute>();
                if (command != null && (command.m_executionLocation & execLoc) == execLoc)
                {
                    var cmd = new CommandInfo() { m_methodObj = methods[i].IsStatic ? null : obj, m_method = methods[i], m_command = command };
                    ValheimMP.Log($"Registered ChatCommand: {GetCommandSyntax(cmd)}");
                    m_commands.Add(cmd);
                }
            }
        }

        public List<CommandInfo> GetCommands()
        {
            return m_commands;
        }

        public void RegisterCommand(object obj, string methodName)
        {
            RegisterCommand(obj, obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }

        public void RegisterCommand(MethodBase method)
        {
            if (!method.IsStatic)
            {
                ValheimMP.Log($"RegisterCommand: Register non static method without instance.");
                return;
            }

            RegisterCommand(null, method);
        }

        public void RegisterCommand(object obj, MethodBase method)
        {
            if (Instance == null)
            {
                ValheimMP.Log($"RegisterCommand: No command manager available.");
                return;
            }

            if (method == null)
            {
                ValheimMP.Log($"RegisterCommand: Missing method.");
                return;
            }

            var command = method.GetCustomAttribute<ChatCommandAttribute>();
            if (command == null)
            {
                ValheimMP.Log($"RegisterCommand: {method.Name} Missing ChatCommandAttribute.");
                return;
            }

            var cmd = new CommandInfo() { m_methodObj = obj, m_method = method, m_command = command };
            ValheimMP.Log($"Registered ChatCommand: {GetCommandSyntax(cmd)}");
            m_commands.Add(cmd);
        }

        internal void OnChatMessage(OnChatMessageArgs args)
        {
            // This normally happens on the client but we supress it here so we can still send marked up text ourselves!
            if (ValheimMP.IsDedicated && !args.Peer.IsAdmin())
            {
                args.Text = args.Text.Replace('<', ' ').Replace('>', ' ');
            }

            if (args.Text.StartsWith(CommandToken))
            {
                // server always suppresses commands even invalid ones.
                args.SuppressMessage = ValheimMP.IsDedicated;
                var text = args.Text.Substring(CommandToken.Length);
                var commandEnd = text.IndexOf(" ");
                if (commandEnd < 0) commandEnd = text.Length;

                var commandText = text.Substring(0, commandEnd);

                foreach (var command in m_commands)
                {
                    if (command.m_command.m_aliases.Contains(commandText))
                    {
                        args.SuppressMessage = true;

                        if (command.m_command.m_requireAdmin && !args.Peer.IsAdmin())
                        {
                            args.Peer.SendServerMessage($"Command {command.m_command.m_name} requires admin access.");
                            break;
                        }

                        try
                        {
                            ExecuteCommand(args, command, text.Substring(commandEnd));
                        }
                        catch (TargetParameterCountException)
                        {
                            SendCommandSyntax(args.Peer, command);
                        }
                        catch (Exception ex)
                        {
                            ValheimMP.Log(ex.ToString());
                            args.Peer.SendServerMessage($"Error in {command.m_command.m_name}: {ex.Message}");
                        }
                        break;
                    }
                }
            }
        }

        private void SendCommandSyntax(ZNetPeer peer, CommandInfo command)
        {
            peer.SendServerMessage(GetCommandSyntax(command));
        }

        public static string GetCommandSyntax(CommandInfo command)
        {
            List<string> paramstr = new();

            foreach (var param in command.m_method.GetParameters())
            {
                if (param.Name == "peer" && param.ParameterType == typeof(ZNetPeer))
                    continue;
                if (param.Name == "player" && param.ParameterType == typeof(Player))
                    continue;
                if (param.Name == "chatargs" && param.ParameterType == typeof(OnChatMessageArgs))
                    continue;

                var argstr = $"{param.ParameterType.Name}: {param.Name}";

                if (param.IsOptional)
                {
                    argstr = $"[{argstr} = {param.DefaultValue ?? "null"}]";
                }

                paramstr.Add(argstr);
            }

            var str = CommandToken + command.m_command.m_name + " " + paramstr.Join();
            return str;
        }

        public void ExecuteCommand(OnChatMessageArgs args, CommandInfo command, string parameters)
        {
            var parameterObjects = new List<object>();
            var parameterInfo = command.m_method.GetParameters();
            var commandParameters = new CommandParameters(parameters);

            for (int i = 0; i < parameterInfo.Length; i++)
            {
                var param = parameterInfo[i];

                if (param.Name == "peer" && param.ParameterType == typeof(ZNetPeer))
                {
                    parameterObjects.Add(args.Peer);
                }
                else if (param.Name == "player" && param.ParameterType == typeof(Player))
                {
                    parameterObjects.Add(args.Player);
                }
                else if (param.Name == "chatargs" && param.ParameterType == typeof(OnChatMessageArgs))
                {
                    parameterObjects.Add(args);
                }
                else
                {
                    // string as last parameter takes in the rest of the string
                    if (i == parameterInfo.Length - 1 && param.ParameterType == typeof(string))
                    {
                        parameterObjects.Add(commandParameters.GetRemainingParameter().TrimStart(' '));
                    }
                    else
                    {
                        var paramValue = commandParameters.GetNextParameter();
                        if (paramValue == null)
                        {
                            if (param.IsOptional)
                            {
                                parameterObjects.Add(param.DefaultValue);
                                continue;
                            }
                            throw new TargetParameterCountException();
                        }

                        if (param.IsOptional && param.ParameterType != typeof(string) && string.IsNullOrEmpty(paramValue))
                            continue;

                        // basic types
                        if (param.ParameterType == typeof(string)) parameterObjects.Add(paramValue);
                        else if (param.ParameterType == typeof(double)) parameterObjects.Add(double.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(double?)) parameterObjects.Add(double.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(float)) parameterObjects.Add(float.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(float?)) parameterObjects.Add(float.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(sbyte)) parameterObjects.Add(sbyte.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(sbyte?)) parameterObjects.Add(sbyte.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(short)) parameterObjects.Add(short.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(short?)) parameterObjects.Add(short.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(int)) parameterObjects.Add(int.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(int?)) parameterObjects.Add(int.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(long)) parameterObjects.Add(long.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(long?)) parameterObjects.Add(long.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(byte)) parameterObjects.Add(byte.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(byte?)) parameterObjects.Add(byte.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(ushort)) parameterObjects.Add(ushort.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(ushort?)) parameterObjects.Add(ushort.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(uint)) parameterObjects.Add(uint.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(uint?)) parameterObjects.Add(uint.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(ulong)) parameterObjects.Add(ulong.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(ulong?)) parameterObjects.Add(ulong.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));

                        // Specially parsed types
                        else if (param.ParameterType == typeof(ZNetPeer))
                        {
                            var paramPeer = ZNet.instance.GetPeerByPlayerName(paramValue, true);
                            if (paramPeer != null)
                            {
                                parameterObjects.Add(paramPeer);
                                continue;
                            }

                            paramPeer = ZNet.instance.GetPeerByHostName(paramValue);
                            if (paramPeer != null)
                            {
                                parameterObjects.Add(paramPeer);
                                continue;
                            }

                            if (param.IsOptional)
                            {
                                commandParameters.ReturnParameter();
                                parameterObjects.Add(param.DefaultValue);
                                continue;
                            }
                        }

                        else if (param.ParameterType == typeof(Player))
                        {
                            var paramPeer = ZNet.instance.GetPeerByPlayerName(paramValue, true);
                            if (paramPeer != null)
                            {
                                parameterObjects.Add(paramPeer.GetPlayer());
                                continue;
                            }

                            paramPeer = ZNet.instance.GetPeerByHostName(paramValue);
                            if (paramPeer != null)
                            {
                                parameterObjects.Add(paramPeer.GetPlayer());
                                continue;
                            }

                            if (param.IsOptional)
                            {
                                commandParameters.ReturnParameter();
                                parameterObjects.Add(param.DefaultValue);
                                continue;
                            }
                        }
                    }
                }
            }

            command.m_method.Invoke(command.m_methodObj, parameterObjects.ToArray());
        }


        public class CommandParameters
        {
            static readonly Regex m_regex = new("(\\s*\"(?<qarg>[^\"]*)\"\\s*|\\s*(?<arg>[^,]+)\\s*)");
            readonly string m_parameters;
            int m_regexpos;
            int m_lastRegexpos;

            public CommandParameters(string parameters)
            {
                m_parameters = parameters;
                m_regexpos = 0;
                m_lastRegexpos = 0;
            }

            public string GetNextParameter()
            {
                var currentParameter = m_regex.Match(m_parameters, m_regexpos);
                var paramGroup = currentParameter.Groups["qarg"];
                if (paramGroup.Success)
                {
                    m_lastRegexpos = m_regexpos;
                    m_regexpos = currentParameter.Index + currentParameter.Length;
                    return paramGroup.Value;
                }


                // non quoted args cannot be empty e.g.: ,, results in null parameters instead of empty string parameters
                // if a empty string needs to be specified it will need to be specifically quoted e.g.: "","",""
                paramGroup = currentParameter.Groups["arg"];
                if (!paramGroup.Success || string.IsNullOrWhiteSpace(paramGroup.Value))
                {
                    return null;
                }

                m_lastRegexpos = m_regexpos;
                m_regexpos = currentParameter.Index + currentParameter.Length;
                return paramGroup.Value.Trim();
            }

            public string GetRemainingParameter()
            {
                return m_parameters.Substring(m_regexpos);
            }

            public void ReturnParameter()
            {
                m_regexpos = m_lastRegexpos;
            }
        }

    }
}
