﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Syncless.CompareAndSync.Enum;

namespace Syncless.CompareAndSync.XMLWriteObject
{
    public abstract class BaseXMLWriteObject
    {
        private string _name, _fullPath, _newName;
        private long _creationTime;
        private MetaChangeType _changeType;
        private long _metaUpdated;

        public BaseXMLWriteObject(string name, string fullPath, MetaChangeType changeType, long metaUpdated)
        {
            _name = name;
            _fullPath = fullPath;
            _changeType = changeType;
            _metaUpdated = metaUpdated;
        }

        public BaseXMLWriteObject(string name, string fullPath, long creationTime, MetaChangeType changeType, long metaUpdated)
            : this(name, fullPath, changeType, metaUpdated)
        {
            _creationTime = creationTime;
        }

        public BaseXMLWriteObject(string name, string newName, string fullPath, long creationTime, MetaChangeType changeType, long metaUpdated)
            : this(name, fullPath, creationTime, changeType, metaUpdated)
        {
            _newName = newName;
        }

        public string Name
        {
            get { return _name; }
        }

        public string FullPath
        {
            get { return _fullPath; }
        }

        public long CreationTime
        {
            get { return _creationTime; }
        }

        public MetaChangeType ChangeType
        {
            get { return _changeType; }
        }

        public string NewName
        {
            get { return _newName; }
        }

        public long MetaUpdated
        {
            get { return _metaUpdated; }
        }

    }
}
