﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Syncless.Tagging;

namespace Syncless.CompareAndSync
{
    public class Comparer
    {
        private const int CREATE_TABLE = 0, DELETE_TABLE = 1, RENAME_TABLE = 2, UPDATE_TABLE = 3;
        private Dictionary<int, Dictionary<string, List<string>>> _changeTable;
        private List<string> deleteList;
        private const string METADATAFOLDER = "_syncless\\";

        public List<CompareResult> CompareFolder(string tagName, List<string> paths)
        {
            _changeTable = new Dictionary<int, Dictionary<string, List<string>>>();
            _changeTable.Add(CREATE_TABLE, new Dictionary<string, List<string>>());
            _changeTable.Add(UPDATE_TABLE, new Dictionary<string, List<string>>());
            //_changeTable.Add(DELETE_TABLE, new Dictionary<string, List<string>>());
            _changeTable.Add(RENAME_TABLE, new Dictionary<string, List<string>>());
            deleteList = new List<string>();

            List<string> withMeta = new List<string>();
            List<string> noMeta = new List<string>();
            string metaPath = Path.Combine(METADATAFOLDER, tagName + ".xml");

            foreach (string path in paths)
            {
                if (File.Exists(Path.Combine(path, metaPath)))
                {
                    withMeta.Add(path);
                }
                else
                {
                    noMeta.Add(path);
                }
            }

            List<CompareInfoObject> mostUpdated = null;

            // YC: If there is only 1 folder with metadata we have nothing to compare
            if (withMeta.Count < 2)
            {
                mostUpdated = DoRawCompareFolder(paths);
            }
            else
            {
                // YC: We compare the folders with metadata first, then
                // we do a raw compare for folders without metadata
                mostUpdated = DoOptimizedCompareFolder(metaPath, withMeta, noMeta);
            }

            return ProcessRawResults();

        }

        /// <summary>
        /// Compare folders against their respective metadata first, then proceed
        /// to compare them against one another.
        /// </summary>
        /// <param name="withMeta">List of folders with metadata</param>
        /// <param name="noMeta">LIst of folders without metadata</param>
        /// <returns>The most updated files across all folders</returns>
        private List<CompareInfoObject> DoOptimizedCompareFolder(string metaPath, List<string> withMeta, List<string> noMeta)
        {
            // YC: Handle metadata and differences
            List<CompareInfoObject> currSrcFolder = GetDiffMetaActual(metaPath, withMeta[0]);

            for (int i = 1; i < withMeta.Count; i++)
            {
                currSrcFolder = DoOptimizedOneWayCompareFolder(currSrcFolder, GetDiffMetaActual(metaPath, withMeta[i]), withMeta[i]);
            }
            for (int i = withMeta.Count - 2; i >= 0; i--)
            {
                currSrcFolder = DoOptimizedOneWayCompareFolder(currSrcFolder, GetDiffMetaActual(metaPath, withMeta[i]), withMeta[i]);
            }

            // YC: Create a virtual most-updated folder for comparison against the folders with no metadata
            // There is a chance that the folders with no metadata have more updated stuff than the folders
            // with metadata
            currSrcFolder = currSrcFolder.Union(GetAllCompareObjects(withMeta[0])).ToList<CompareInfoObject>();

            for (int i = 0; i < noMeta.Count; i++)
            {
                currSrcFolder = DoRawOneWayCompareFolder(currSrcFolder, GetAllCompareObjects(noMeta[i]), noMeta[i], withMeta);
            }

            for (int i = noMeta.Count - 2; i >= 0; i--)
            {
                currSrcFolder = DoRawOneWayCompareFolder(currSrcFolder, GetAllCompareObjects(noMeta[i]), noMeta[i], withMeta);
            }

            currSrcFolder = DoRawOneWayCompareFolder(currSrcFolder, GetAllCompareObjects(withMeta[0]), withMeta[0], withMeta);

            return currSrcFolder;
        }

        private List<CompareInfoObject> GetDiffMetaActual(string metaPath, string path)
        {
            //Do some processing between meta and actual files
            List<CompareInfoObject> results = new List<CompareInfoObject>();

            List<CompareInfoObject> actual = GetAllCompareObjects(path);
            List<CompareInfoObject> meta = GetMetadataCompareObjects(metaPath, path);

            // YC: This will give us files that exist but are not in the
            // metadata. Implies either new or renamed files.
            List<CompareInfoObject> actualExceptMeta = actual.Except<CompareInfoObject>(meta, new FileNameCompare()).ToList<CompareInfoObject>();

            // YC: This will give us files that exist in metadata but in the
            // actual folder. Implies either deleted or renamed files.
            List<CompareInfoObject> metaExceptActual = meta.Except<CompareInfoObject>(actual, new FileNameCompare()).ToList<CompareInfoObject>();
            bool rename = false;
            List<string> renameList = null;
            List<string> deleteList = null;
            CompareInfoObject tempMeta = null;

            foreach (CompareInfoObject a in actualExceptMeta)
            {
                rename = false;
                foreach (CompareInfoObject m in meta)
                {
                    if (a.MD5Hash == m.MD5Hash)
                    {
                        rename = true;
                        tempMeta = m;
                        break;
                    }
                }
                if (rename)
                {
                    if (_changeTable[RENAME_TABLE].TryGetValue(tempMeta.RelativePathToOrigin, out renameList))
                    {
                        if (!renameList.Contains(a.RelativePathToOrigin))
                        {
                            renameList.Add(a.RelativePathToOrigin);
                        }
                    }
                    else
                    {
                        renameList = new List<string>();
                        renameList.Add(a.RelativePathToOrigin);
                        _changeTable[RENAME_TABLE].Add(tempMeta.RelativePathToOrigin, renameList);
                    }
                }
                else
                {
                    //add to new/updated list
                    results.Add(a);
                }
            }

            foreach (CompareInfoObject a in metaExceptActual)
            {
                rename = false;
                foreach (CompareInfoObject m in meta)
                {
                    if (a.MD5Hash == m.MD5Hash)
                    {
                        rename = true;
                        tempMeta = m;
                        break;
                    }
                }
                if (rename)
                {
                    if (_changeTable[RENAME_TABLE].TryGetValue(tempMeta.RelativePathToOrigin, out renameList))
                    {
                        if (!renameList.Contains(a.RelativePathToOrigin))
                        {
                            renameList.Add(a.RelativePathToOrigin);
                        }
                    }
                    else
                    {
                        renameList = new List<string>();
                        renameList.Add(a.RelativePathToOrigin);
                        _changeTable[RENAME_TABLE].Add(tempMeta.RelativePathToOrigin, renameList);
                    }
                }
                else
                {
                    if (!deleteList.Contains(tempMeta.RelativePathToOrigin)) {
                        deleteList.Add(tempMeta.RelativePathToOrigin);
                    }
                }
            }

            List<CompareInfoObject> actualIntersectMeta = actual.Intersect<CompareInfoObject>(meta, new FileNameCompare()).ToList<CompareInfoObject>();
            List<CompareInfoObject> metaIntersectActual = meta.Intersect<CompareInfoObject>(actual, new FileNameCompare()).ToList<CompareInfoObject>();
            Debug.Assert(actualIntersectMeta.Count == metaIntersectActual.Count);
            int numOfCommonItems = actualIntersectMeta.Count;
            CompareInfoObject actualFile = null;
            CompareInfoObject metaFile = null;
            int compareResult = 0;

            for (int i = 0; i < numOfCommonItems; i++)
            {
                actualFile = (CompareInfoObject)actualIntersectMeta[i];
                metaFile = (CompareInfoObject)metaIntersectActual[i];
                compareResult = new FileContentCompare().Compare(actualFile, metaFile);

                if (actualFile.Length != metaFile.Length)
                {
                    results.Add(actualFile);
                    continue;
                }
                if (actualFile.MD5Hash != metaFile.MD5Hash)
                {
                    results.Add(actualFile);
                    continue;
                }
            }

            return results;
        }

        private List<CompareInfoObject> DoOptimizedOneWayCompareFolder(List<CompareInfoObject> source, List<CompareInfoObject> target, string targetPath)
        {
            //TODO
            return null;
        }

        /// <summary>
        /// Compare folders regardless of metadata. It can only handle update and create changes,
        /// not delete and rename since the metadata is not used.
        /// </summary>
        /// <param name="paths">List of folders</param>
        /// <returns>The most updated files across all folders</returns>
        private List<CompareInfoObject> DoRawCompareFolder(List<string> paths)
        {            
            List<CompareInfoObject> currSrcFolder = GetAllCompareObjects(paths[0]);

            for (int i = 1; i < paths.Count; i++)
            {
                currSrcFolder = DoRawOneWayCompareFolder(currSrcFolder, GetAllCompareObjects(paths[i]), paths[i], null);
            }
            for (int i = paths.Count - 2; i >= 0; i--)
            {
                currSrcFolder = DoRawOneWayCompareFolder(currSrcFolder, GetAllCompareObjects(paths[i]), paths[i], null);
            }

            return currSrcFolder;
        }

        private List<CompareInfoObject> DoRawOneWayCompareFolder(List<CompareInfoObject> source, List<CompareInfoObject> target, string targetPath, List<string> withMeta)
        {
            Debug.Assert(source != null && target != null);
            List<CompareInfoObject> querySrcExceptTgt = source.Except(target, new FileNameCompare()).ToList<CompareInfoObject>();
            List<CompareInfoObject> querySrcIntersectTgt = source.Intersect(target, new FileNameCompare()).ToList<CompareInfoObject>();
            List<CompareInfoObject> queryTgtIntersectSrc = target.Intersect(source, new FileNameCompare()).ToList<CompareInfoObject>();
            int exceptItemsCount = querySrcExceptTgt.Count;
            Debug.Assert(querySrcIntersectTgt.Count == queryTgtIntersectSrc.Count);
            int commonItemsCount = queryTgtIntersectSrc.Count;
            List<string> createList, updateList;

            for (int i = 0; i < exceptItemsCount; i++)
            {
                List<string> newFilePaths = new List<string>();
                if (withMeta != null && withMeta.Contains(targetPath))
                {
                    foreach (string withMetaPath in withMeta)
                    {
                        newFilePaths.Add(CreateNewItemPath(querySrcExceptTgt[i], withMetaPath));
                    }
                }
                else
                {
                    newFilePaths.Add(CreateNewItemPath(querySrcExceptTgt[i], targetPath));
                }
                
                if (_changeTable[CREATE_TABLE].TryGetValue(querySrcExceptTgt[i].FullName, out createList))
                {
                    foreach (string newFilePath in newFilePaths)
                    {
                        if (!createList.Contains(newFilePath))
                        {
                            createList.Add(newFilePath);
                        }
                    }
                }
                else
                {
                    createList = new List<string>();
                    foreach (string newFilePath in newFilePaths)
                    {
                        if (!createList.Contains(newFilePath))
                        {
                            createList.Add(newFilePath);
                        }
                    }
                    _changeTable[CREATE_TABLE].Add(querySrcExceptTgt[i].FullName, createList);
                }
            }

            CompareInfoObject srcFile = null;
            CompareInfoObject tgtFile = null;
            int compareResult = 0;

            for (int i = 0; i < commonItemsCount; i++)
            {
                srcFile = (CompareInfoObject)querySrcIntersectTgt[i];
                tgtFile = (CompareInfoObject)queryTgtIntersectSrc[i];
                compareResult = new FileContentCompare().Compare(srcFile, tgtFile);

                if (compareResult > 0)
                {
                    List<string> updatePaths = new List<string>();
                    if (withMeta != null && withMeta.Contains(targetPath))
                    {
                        foreach (string withMetaPath in withMeta)
                        {
                            updatePaths.Add(CreateNewItemPath(tgtFile, withMetaPath));
                        }
                    }
                    else
                    {
                        updatePaths.Add(tgtFile.FullName);
                    }

                    if (_changeTable[UPDATE_TABLE].TryGetValue(srcFile.FullName, out updateList))
                    {
                        foreach (string updatePath in updatePaths)
                        {
                            if (!updateList.Contains(updatePath))
                            {
                                updateList.Add(updatePath);
                            }
                        }
                    }
                    else
                    {
                        updateList = new List<string>();
                        foreach (string updatePath in updatePaths)
                        {
                            updateList.Add(updatePath);
                        }
                        _changeTable[UPDATE_TABLE].Add(srcFile.FullName, updateList);
                    }

                    //YC: Experimental
                    if (_changeTable[CREATE_TABLE].ContainsKey(tgtFile.FullName))
                    {
                        _changeTable[CREATE_TABLE].Remove(tgtFile.FullName);
                    }

                    queryTgtIntersectSrc.RemoveAt(i);
                    queryTgtIntersectSrc.Insert(i, srcFile);
                }
                else if (compareResult < 0)
                {
                    if (_changeTable[UPDATE_TABLE].ContainsKey(srcFile.FullName))
                    {
                        _changeTable[UPDATE_TABLE].Remove(srcFile.FullName);
                    }
                }

            }
            return (queryTgtIntersectSrc.Union(querySrcExceptTgt)).Union(target).ToList<CompareInfoObject>();
        }

        private List<CompareInfoObject> GetAllCompareObjects(string path)
        {
            FileInfo[] allFiles = new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories);
            List<CompareInfoObject> results = new List<CompareInfoObject>();
            foreach (FileInfo f in allFiles)
            {
                results.Add(new CompareInfoObject(path, f.FullName, f.Name, f.LastWriteTime.Ticks, f.Length, CalculateMD5Hash(f)));
            }
            return results;
        }

        /// <summary>
        /// Creates a list of CompareInfoObject based on metadata
        /// </summary>
        /// <param name="path"></param>
        /// <returns>List of CompareInfoObject</returns>
        private List<CompareInfoObject> GetMetadataCompareObjects(string metaPath, string path)
        {
            String s = Path.Combine(path, metaPath);
            Debug.Assert(File.Exists(s));

            //Process XML here

            return null;
        }

        private string CreateNewItemPath(CompareInfoObject source, string targetOrigin)
        {
            Debug.Assert(source != null && targetOrigin != null);            
            return Path.Combine(targetOrigin, source.RelativePathToOrigin);
        }

        private List<CompareResult> ProcessRawResults()
        {
            Dictionary<int, Dictionary<string, List<string>>>.KeyCollection keys = _changeTable.Keys;
            Dictionary<string, List<string>>.KeyCollection currTableKeys = null;
            List<CompareResult> results = new List<CompareResult>();
            FileChangeType changeType = FileChangeType.Create;

            foreach (int key in keys)
            {
                currTableKeys = _changeTable[key].Keys;
                switch (key)
                {
                    case CREATE_TABLE:
                        changeType = FileChangeType.Create;
                        break;
                    case RENAME_TABLE:
                        changeType = FileChangeType.Rename;
                        break;
                    case UPDATE_TABLE:
                        changeType = FileChangeType.Update;
                        break;
                }

                foreach (string sourceKey in currTableKeys)
                {
                    foreach (string dest in _changeTable[key][sourceKey])
                    {
                        results.Add(new CompareResult(changeType, sourceKey, dest));
                    }
                }
            }

            foreach (CompareResult cr in results)
            {
                Console.WriteLine(cr.ToString());
                Console.WriteLine();
            }

            return results;
        }

        public static string CalculateMD5Hash(FileInfo fileInput)
        {
            FileStream fileStream = fileInput.OpenRead();
            byte[] fileHash = MD5.Create().ComputeHash(fileStream);
            fileStream.Close();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fileHash.Length; i++)
            {
                sb.Append(fileHash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Simple file name comparer.
        /// </summary>
        private class FileNameCompare : IEqualityComparer<CompareInfoObject>
        {
            public bool Equals(CompareInfoObject c1, CompareInfoObject c2)
            {
                return (c1.RelativePathToOrigin.ToLower() == c2.RelativePathToOrigin.ToLower());
            }

            public int GetHashCode(CompareInfoObject c)
            {
                return c.RelativePathToOrigin.ToLower().GetHashCode();
            }
        }

        class FileContentCompare : IComparer<CompareInfoObject>
        {
            public int Compare(CompareInfoObject c1, CompareInfoObject c2)
            {
                bool isEqual = true;

                // YC: If file length is different, they are definitely different files
                if (c1.Length != c2.Length)
                    isEqual = false;

                if (isEqual)
                {
                    if (!c1.LastWriteTime.Equals(c2.LastWriteTime))
                    {
                        isEqual = false;
                    }

                    if (!isEqual)
                    {
                        if (c1.MD5Hash != c2.MD5Hash)
                        {
                            isEqual = false;
                        }
                    }
                }

                if (isEqual)
                {
                    return 0;
                }
                else
                {
                    return c1.LastWriteTime.CompareTo(c2.LastWriteTime);
                }
            }
        }
    }
}
