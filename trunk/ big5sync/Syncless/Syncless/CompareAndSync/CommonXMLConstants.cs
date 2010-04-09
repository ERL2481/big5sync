﻿namespace Syncless.CompareAndSync
{
    public static class CommonXMLConstants
    {
        //XML Paths
        public const string MetaDir = ".syncless";
        public const string XMLName = "syncless.xml";
        public const string LastKnownStateName = "lastknownstate.xml";
        public const string MetadataPath = MetaDir + "\\" + XMLName;
        public const string LastKnownStatePath = MetaDir + "\\" + LastKnownStateName;

        //XML Metadata (syncless.xml)
        public const string NodeMetaData = "meta-data";
        public const string NodeName = "name";
        public const string NodeSize = "size";
        public const string NodeHash = "hash";
        public const string NodeLastCreatedUtc = "last_created_utc";

        //Last Known State Metadata (lastknownstate.xml)
        public const string NodeLastKnownState = "last_known_state";
        public const string NodeAction = "action";
        public const string ActionDeleted = "deleted";

        //Common Elements in Metadatas
        public const string NodeFolder = "folder";
        public const string NodeFile = "file";
        public const string NodeLastModifiedUtc = "last_modified_utc";
        public const string NodeLastUpdatedUtc = "last_updated_utc";
        
        //XML Metadata XPath (syncless.xml)
        public const string XPathExpr = "/meta-data";
        public const string XPathName = "/" + NodeName;
        public const string XPathSize = "/" + NodeSize;
        public const string XPathHash = "/" + NodeHash;
        public const string XPathLastCreated = "/" + NodeLastCreatedUtc;

        //Last Known State Metadata XPath
        public const string XPathLastKnownState = "/" + NodeLastKnownState;
        public const string XPathAction = "/" + NodeAction;

        //Common XPath
        public const string XPathFolder = "/" + NodeFolder;
        public const string XPathFile = "/" + NodeFile;
        public const string XPathLastModified = "/" + NodeLastModifiedUtc;
        public const string XPathLastUpdated = "/" + NodeLastUpdatedUtc;

    }

}
