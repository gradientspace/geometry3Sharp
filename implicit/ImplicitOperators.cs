using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    abstract public class ImplicitNAryOp2d : ImplicitOperator2d
    {
        protected List<ImplicitField2d> m_vChildren;

        public ImplicitNAryOp2d()
        {
            m_vChildren = new List<ImplicitField2d>();
        }

        public void AddChild(ImplicitField2d pField)
        {
            m_vChildren.Add(pField);
        }

        virtual public float Value(float fX, float fY)
        {
            return 0;
        }

        virtual public void Gradient(float fX, float fY, ref float fGX, ref float fGY)
        {
            const float fDelta = 0.001f;
            float fValue = Value(fX, fY);
            fGX = (Value(fX + fDelta, fY) - fValue) / fDelta;
            fGY = (Value(fX, fY + fDelta) - fValue) / fDelta;
        }

        virtual public AxisAlignedBox2f Bounds
        {
            get {
                AxisAlignedBox2f box = new AxisAlignedBox2f();
                for (int i = 0; i < m_vChildren.Count; ++i)
                    box.Contain(m_vChildren[i].Bounds);
                return box;
            }
        }
    }

    public class ImplicitBlend2d : ImplicitNAryOp2d
    {
        public ImplicitBlend2d() : base()
        {
        }

        override public float Value(float fX, float fY)
        {
            float fSumValue = 0.0f;
            foreach (ImplicitField2d child in m_vChildren)
                fSumValue += child.Value(fX, fY);
            return fSumValue;
        }

        override public void Gradient(float fX, float fY, ref float fGX, ref float fGY)
        {
            fGX = fGY = 0;
            float fTempX = 0, fTempY = 0;
            foreach (ImplicitField2d child in m_vChildren) {
                child.Gradient(fX, fY, ref fTempX, ref fTempY);
                fGX += fTempX;
                fGY += fTempY;
            }
        }
    }

    public class ImplicitUnion2d : ImplicitNAryOp2d
    {
        public ImplicitUnion2d() : base()
        {
        }

        override public float Value(float fX, float fY)
        {
            float fMaxValue = 0.0f;
            foreach (ImplicitField2d child in m_vChildren)
                fMaxValue = Math.Max(fMaxValue, child.Value(fX, fY));
            return fMaxValue;
        }

        override public void Gradient(float fX, float fY, ref float fGX, ref float fGY)
        {
            float fMaxValue = 0.0f;
            int nMax = -1;
            for (int i = 0; i < m_vChildren.Count; ++i) {
                float fValue = m_vChildren[i].Value(fX, fY);
                if (fValue > fMaxValue) {
                    nMax = i;
                    fMaxValue = fValue;
                }
            }
            if (nMax >= 0)
                m_vChildren[nMax].Gradient(fX, fY, ref fGX, ref fGY);
            else {
                fGX = fGY = 0;
            }
        }
    }


    public class ImplicitIntersection2d : ImplicitNAryOp2d
    {
        public ImplicitIntersection2d()
        {
        }

        override public float Value(float fX, float fY)
        {
            float fMinValue = 9999999999.0f;
            foreach (ImplicitField2d child in m_vChildren)
                fMinValue = Math.Min(fMinValue, child.Value(fX, fY));
            return fMinValue;
        }

        override public void Gradient(float fX, float fY, ref float fGX, ref float fGY)
        {
            float fMinValue = 9999999999.0f;
            int nMin = -1;
            for (int i = 0; i < m_vChildren.Count; ++i) {
                float fValue = m_vChildren[i].Value(fX, fY);
                if (fValue < fMinValue)
                    nMin = i;
                fMinValue = fValue;
            }
            if (nMin >= 0)
                m_vChildren[nMin].Gradient(fX, fY, ref fGX, ref fGY);
            else {
                fGX = fGY = 0;
            }
        }
    }


    public class ImplicitDifference2d : ImplicitNAryOp2d
    {
        public ImplicitDifference2d()
        {
        }

        override public float Value(float fX, float fY)
        {
            if (m_vChildren.Count <= 0)
                return 0;
            float fCurValue = m_vChildren[0].Value(fX, fY);

            for (int i = 1; i < m_vChildren.Count; ++i) {
                float fValue = 1.0f - m_vChildren[i].Value(fX, fY);
                if (fValue < fCurValue)
                    fCurValue = fValue;
            }
            return fCurValue;
        }

        override public void Gradient(float fX, float fY, ref float fGX, ref float fGY)
        {
            if (m_vChildren.Count <= 0) {
                fGX = fGY = 0;
                return;
            }

            int nMin = 0;
            float fCurValue = m_vChildren[0].Value(fX, fY);

            for (int i = 1; i < m_vChildren.Count; ++i) {
                float fValue = 1.0f - m_vChildren[i].Value(fX, fY);
                if (fValue < fCurValue) {
                    nMin = i;
                    fCurValue = fValue;
                }
            }

            m_vChildren[nMin].Gradient(fX, fY, ref fGX, ref fGY);
            if (nMin > 0) {
                fGX = -fGX;
                fGY = -fGY;
            }
        }
    }
}
