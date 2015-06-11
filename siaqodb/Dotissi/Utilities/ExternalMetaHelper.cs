﻿using System;

using System.Reflection;
using Dotissi.PropertyResolver;
using Sqo.Exceptions;


namespace Dotissi.Utilities
{
    public  static class ExternalMetaHelper
    {
        public static string GetBackingField(MemberInfo mi)
        {

            System.Reflection.PropertyInfo pi = mi as System.Reflection.PropertyInfo;
			#if SILVERLIGHT || CF || UNITY3D || WinRT || MONODROID
            string fieldName = SilverlightPropertyResolver.GetPrivateFieldName(pi, pi.DeclaringType);
             if (fieldName != null)
            {
                return fieldName;
            }
#else
            System.Reflection.FieldInfo fInfo = BackingFieldResolver.GetBackingField(pi);
           
            if (fInfo != null)
            {
                return fInfo.Name;
            }
#endif
            else
            {
                string fld = Dotissi.Utilities.MetaHelper.GetBackingFieldByAttribute(mi);
                if (fld != null)
                {

                    return fld;
                }
                else
                {
                    throw new SiaqodbException("A Property must have UseVariable Attribute set");
                }
            }



        }
    }
}
