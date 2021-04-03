using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.ChatCommands
{
    public class ChatCommandManager
    {
        public struct CommandInfo
        {
            internal object m_methodObj;
            internal MethodBase m_method;
            internal ChatCommandAttribute m_command;
        };

        internal List<CommandInfo> m_commands;

        public static ChatCommandManager Instance { get; private set; }

        public static string CommandToken { get; } = "/";

        public ChatCommandManager()
        {
            m_commands = new List<CommandInfo>();
            Instance = this;
        }

        public void RegisterAll(object obj)
        {
            var methods = obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var command = methods[i].GetCustomAttribute<ChatCommandAttribute>();
                if (command != null)
                {
                    var cmd = new CommandInfo() { m_methodObj = methods[i].IsStatic ? null : obj, m_method = methods[i], m_command = command };
                    ChatCommands.Log($"Registered ChatCommand: {GetCommandSyntax(cmd)}");
                    m_commands.Add(cmd);
                }
            }
        }

        public void RegisterCommand(object obj, string methodName)
        {
            RegisterCommand(obj, obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }

        public void RegisterCommand(MethodBase method)
        {
            if(!method.IsStatic)
            {
                ChatCommands.Log($"RegisterCommand: Register non static method without instance.");
                return;
            }
            
            RegisterCommand(null, method);
        }

        public void RegisterCommand(object obj, MethodBase method)
        {
            if (Instance == null)
            {
                ChatCommands.Log($"RegisterCommand: No command manager available.");
                return;
            }

            if (method == null)
            {
                ChatCommands.Log($"RegisterCommand: Missing method.");
                return;
            }

            var command = method.GetCustomAttribute<ChatCommandAttribute>();
            if (command == null)
            {
                ChatCommands.Log($"RegisterCommand: {method.Name} Missing ChatCommandAttribute.");
                return;
            }

            var cmd = new CommandInfo() { m_methodObj = obj, m_method = method, m_command = command };
            ChatCommands.Log($"Registered ChatCommand: {GetCommandSyntax(cmd)}");
            m_commands.Add(cmd);
        }

        internal bool OnChatMessage(ZNetPeer peer, Player player, ref string playerName, ref Vector3 messageLocation, ref float messageDistance, ref string text, ref Talker.Type type)
        {
            // This normally happens on the client but we supress it here so we can still send marked up text ourselves!
            text = text.Replace('<', ' ');
            text = text.Replace('>', ' ');

            if (text.StartsWith(CommandToken))
            {
                text = text.Substring(CommandToken.Length);
                var commandEnd = text.IndexOf(" ");
                if (commandEnd < 0) commandEnd = text.Length;

                var commandText = text.Substring(0, commandEnd);

                foreach (var command in m_commands)
                {
                    if (command.m_command.m_aliases.Contains(commandText))
                    {
                        if(command.m_command.m_requireAdmin && !peer.IsAdmin())
                        {
                            peer.SendServerMessage($"Command {command.m_command.m_name} requires admin access.");
                            break;
                        }

                        try
                        {
                            ExecuteCommand(peer, player, command, text.Substring(commandEnd));
                        }
                        catch (TargetParameterCountException)
                        {
                            SendCommandSyntax(peer, player, command);
                        }
                        catch (Exception ex)
                        {
                            peer.SendServerMessage($"Error in {command.m_command.m_name}: {ex.Message}");
                            ChatCommands.Log(ex.ToString());
                        }
                        break;
                    }
                }

                return false;
            }

            return true;
        }

        private void SendCommandSyntax(ZNetPeer peer, Player player, CommandInfo command)
        {
            string str = GetCommandSyntax(command);

            peer.SendServerMessage(CommandToken + str);
        }

        private static string GetCommandSyntax(CommandInfo command)
        {
            List<string> paramstr = new();

            foreach (var param in command.m_method.GetParameters())
            {
                if (param.Name == "peer" && param.ParameterType == typeof(ZNetPeer))
                    continue;
                if (param.Name == "player" && param.ParameterType == typeof(Player))
                    continue;

                var argstr = $"{param.ParameterType.Name}: {param.Name}";

                if (param.IsOptional)
                {
                    argstr = $"[{argstr} = {param.DefaultValue ?? "null"}]";
                }

                paramstr.Add(argstr);
            }

            var str = command.m_command.m_name + " " + paramstr.Join();
            return str;
        }

        public void ExecuteCommand(ZNetPeer peer, Player player, CommandInfo command, string parameters)
        {
            var parameterObjects = new List<object>();
            var parameterInfo = command.m_method.GetParameters();
            var commandParameters = new CommandParameters(parameters);

            for (int i = 0; i < parameterInfo.Length; i++)
            {
                var param = parameterInfo[i];

                if (param.Name == "peer" && param.ParameterType == typeof(ZNetPeer))
                {
                    parameterObjects.Add(peer);
                }
                else if (param.Name == "player" && param.ParameterType == typeof(Player))
                {
                    parameterObjects.Add(player);
                }
                else
                {
                    // string as last parameter takes in the rest of the string
                    if (i == parameterInfo.Length - 1 && param.ParameterType == typeof(string))
                    {
                        parameterObjects.Add(commandParameters.GetRemainingParameter());
                    }
                    else
                    {
                        var paramValue = commandParameters.GetNextParameter();
                        if (paramValue == null)
                        {
                            if (param.IsOptional)
                            {
                                parameterObjects.Add(param.DefaultValue);
                                break;
                            }
                            throw new TargetParameterCountException();
                        }

                        if (param.IsOptional && param.ParameterType != typeof(string) && string.IsNullOrEmpty(paramValue))
                            continue;

                        // basic types
                        if (param.ParameterType == typeof(string)) parameterObjects.Add(paramValue);
                        else if (param.ParameterType == typeof(double)) parameterObjects.Add(double.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(float)) parameterObjects.Add(float.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(sbyte)) parameterObjects.Add(sbyte.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(short)) parameterObjects.Add(short.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(int)) parameterObjects.Add(int.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(long)) parameterObjects.Add(long.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(byte)) parameterObjects.Add(byte.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(ushort)) parameterObjects.Add(ushort.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(uint)) parameterObjects.Add(uint.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));
                        else if (param.ParameterType == typeof(ulong)) parameterObjects.Add(ulong.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture));

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


        internal class CommandParameters
        {
            static readonly Regex m_regex = new Regex(@"(\""(?<qarg>[^""]*)\""|(?<arg>[^\s]+))");
            readonly string m_parameters;
            int m_regexpos;
            int m_lastRegexpos;

            internal CommandParameters(string parameters)
            {
                m_parameters = parameters;
                m_regexpos = 0;
                m_lastRegexpos = 0;
            }

            internal string GetNextParameter()
            {
                var currentParameter = m_regex.Match(m_parameters, m_regexpos);
                var paramGroup = currentParameter.Groups["qarg"].Success ? currentParameter.Groups["qarg"] : currentParameter.Groups["arg"];
                if (!paramGroup.Success)
                {
                    return null;
                }
                m_lastRegexpos = m_regexpos;
                m_regexpos = paramGroup.Index + paramGroup.Length;
                return paramGroup.Value;
            }

            internal string GetRemainingParameter()
            {
                return m_parameters.Substring(m_regexpos);
            }

            internal void ReturnParameter()
            {
                m_regexpos = m_lastRegexpos;
            }
        }

    }
}
