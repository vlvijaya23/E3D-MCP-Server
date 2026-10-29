using System;
using System.Collections.Generic;
using System.IO;

namespace E3DBridge.Staad
{
    public static class StaadParser
    {
        public static string ReadFile(
            string filePath)
        {
            return File.ReadAllText(
                filePath);
        }

        public static List<StaadNode> GetNodes(
            string filePath)
        {
            List<StaadNode> nodes =
                new List<StaadNode>();

            string text =
                File.ReadAllText(
                    filePath);

            string[] lines =
                text.Split('\n');

            bool insideJoints = false;

            foreach (string line in lines)
            {
                string cleanLine =
                    line.Trim();

                if (cleanLine.StartsWith("JOINT COORDINATES"))
                {
                    insideJoints = true;
                    continue;
                }

                if (insideJoints &&
                    cleanLine.StartsWith("MEMBER INCIDENCES"))
                {
                    break;
                }

                if (insideJoints)
                {
                    string[] entries =
                        cleanLine.Split(';');

                    foreach (string entry in entries)
                    {
                        string nodeLine =
                            entry.Trim();

                        if (nodeLine.Length == 0)
                        {
                            continue;
                        }

                        string[] parts =
                            nodeLine.Split(
                                new char[] { ' ' },
                                StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length >= 4)
                        {
                            StaadNode node =
                                new StaadNode();

                            double x;
                            double y;
                            double z;
                            int id;

                            if (int.TryParse(parts[0], out id) &&
                                double.TryParse(parts[1], out x) &&
                                double.TryParse(parts[2], out y) &&
                                double.TryParse(parts[3], out z))
                            {
                                node.Id = id;
                                node.X = x;
                                node.Y = y;
                                node.Z = z;

                                nodes.Add(node);
                            }
                        }
                    }
                }
            }

            return nodes;
        }

        public static List<StaadMember> GetMembers(
            string filePath)
        {
            List<StaadMember> members =
                new List<StaadMember>();

            string text =
                File.ReadAllText(
                    filePath);

            string[] lines =
                text.Split('\n');

            bool insideMembers = false;

            foreach (string line in lines)
            {
                string cleanLine =
                    line.Trim();

                if (cleanLine.StartsWith("MEMBER INCIDENCES"))
                {
                    insideMembers = true;
                    continue;
                }

                if (insideMembers &&
                    cleanLine.StartsWith("DEFINE"))
                {
                    break;
                }

                if (!insideMembers)
                {
                    continue;
                }

                string[] entries =
                    cleanLine.Split(';');

                foreach (string entry in entries)
                {
                    string memberLine =
                        entry.Trim();

                    if (memberLine.Length == 0)
                    {
                        continue;
                    }

                    string[] parts =
                        memberLine.Split(
                            new char[] { ' ' },
                            StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 3)
                    {
                        continue;
                    }

                    int id;
                    int startNode;
                    int endNode;

                    if (!int.TryParse(parts[0], out id))
                    {
                        continue;
                    }

                    if (!int.TryParse(parts[1], out startNode))
                    {
                        continue;
                    }

                    if (!int.TryParse(parts[2], out endNode))
                    {
                        continue;
                    }

                    StaadMember member =
                        new StaadMember();

                    member.Id = id;
                    member.StartNode = startNode;
                    member.EndNode = endNode;

                    members.Add(member);
                }
            }

            return members;
        }
    }
}