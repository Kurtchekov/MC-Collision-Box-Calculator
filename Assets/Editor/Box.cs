using System;

[Serializable]
public class Box {
    public byte x0, y0, z0, x1, y1, z1;

    public Box() { }

    public Box(byte x0, byte y0, byte z0, byte x1, byte y1, byte z1) {
        this.x0 = x0;
        this.y0 = y0;
        this.z0 = z0;
        this.x1 = x1;
        this.y1 = y1;
        this.z1 = z1;
    }

    public override string ToString() {
        return ("[" + string.Join(",", x0, y0, z0, x1, y1, z1) + "]");
    }

    public string ToString(string format) {
        return ("[" + string.Format(format, x0, y0, z0, x1, y1, z1, 16-x0, 16-y0, 16-z0, 16-x1, 16-y1, 16-z1) + "]");
    }

    public static bool operator ==(Box box0, Box box1) {
        return (
            box0.x0 == box1.x0 && box0.y0 == box1.y0 && box0.z0 == box1.z0 &&
            box0.x1 == box1.x1 && box0.y1 == box1.y1 && box0.z1 == box1.z1);
    }

    public static bool operator !=(Box box0, Box box1) {
        return (box0.x0 != box1.x0 || box0.y0 != box1.y0 || box0.z0 != box1.z0 ||
            box0.x1 != box1.x1 || box0.y1 != box1.y1 || box0.z1 != box1.z1);
    }

    public override bool Equals(object obj) {
        return (obj is Box box1 && this == box1);
    }

    public override int GetHashCode() {
        int hashCode = 1656437989;
        hashCode = hashCode * -1521134295 + x0.GetHashCode();
        hashCode = hashCode * -1521134295 + y0.GetHashCode();
        hashCode = hashCode * -1521134295 + z0.GetHashCode();
        hashCode = hashCode * -1521134295 + x1.GetHashCode();
        hashCode = hashCode * -1521134295 + y1.GetHashCode();
        hashCode = hashCode * -1521134295 + z1.GetHashCode();
        return hashCode;
    }
}
