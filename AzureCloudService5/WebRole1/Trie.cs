using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace WebRole1
{
     public class Trie
        {
            private TrieNode head;
            List<string> returnList;

            public Trie()
            {
                head = new TrieNode();
                returnList = new List<string>();
            }

            public List<string> searchAll(string input)
            {
                StringBuilder sb = new StringBuilder();
                TrieNode currentNode = head;
                TrieNode child = null;


                char[] chars = input.ToCharArray();

                for (int counter = 0; counter < chars.Length; counter++)
                {
                    if (counter < chars.Length - 1)
                    {
                        sb.Append(chars[counter]);
                    }

                    child = currentNode.GetChild(chars[counter]);

                    if (child == null)  // At the end, we're done here
                    {
                        break;
                    }

                    currentNode = child;
                }
                returnList.Clear();
                Create_Branches(currentNode, sb.ToString(), input);

                return returnList;
            }

            private void Create_Branches(TrieNode node, string sub_string, string input)
            {
                if (node == null || returnList.Count >= 10)
                {
                    return;
                }

                sub_string = sub_string + node.data;


                if (node.count > 0 && sub_string.StartsWith(input, StringComparison.OrdinalIgnoreCase))  // Until we've reached the end of the word, keep adding
                {
                    returnList.Add(sub_string);
                }

                foreach (var n in node.children)   // Recursively generate branches for the subnodes
                {
                    Create_Branches(n, sub_string, input);
                }
            }

            /**
             * Add a word to the trie.
             * Adding is O (|A| * |W|), where A is the alphabet and W is the word being searched.
             */
            public void AddWord(string word)
            {
                TrieNode curr = head;

                curr = curr.GetChild(word[0], true);

                for (int i = 1; i < word.Length; i++)
                {
                    curr = curr.GetChild(word[i], true);
                }

                curr.AddCount();
            }

            /**
             * Get the count of a partictlar word.
             * Retrieval is O (|A| * |W|), where A is the alphabet and W is the word being searched.
             */
            public int GetCount(string word)
            {
                TrieNode curr = head;

                foreach (char c in word)
                {
                    curr = curr.GetChild(c);

                    if (curr == null)
                    {
                        return 0;
                    }
                }

                return curr.count;
            }

            internal class TrieNode
            {
                public LinkedList<TrieNode> children { set; get; }

                public int count { private set; get; }
                public char data { private set; get; }

                public TrieNode(char data = ' ')
                {
                    this.data = data;
                    count = 0;
                    children = new LinkedList<TrieNode>();
                }

                public TrieNode GetChild(char c, bool createIfNotExist = false)
                {
                    foreach (var child in children)
                    {
                        if (child.data == c)
                        {
                            return child;
                        }
                    }

                    if (createIfNotExist)
                    {
                        return CreateChild(c);
                    }


                    return null;
                }

                public void AddCount()
                {
                    count++;
                }

                public TrieNode CreateChild(char c)
                {
                    var child = new TrieNode(c);
                    children.AddLast(child);

                    return child;
                }
            }
        }

}