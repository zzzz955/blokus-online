import bcrypt from 'bcryptjs';
import jwt from 'jsonwebtoken';
import { prisma } from './prisma';

const JWT_SECRET = process.env.JWT_SECRET || 'fallback-secret';

export interface JWTPayload {
  userId: number;
  username: string;
  role: string;
}

export async function hashPassword(password: string): Promise<string> {
  return bcrypt.hash(password, 12);
}

export async function verifyPassword(password: string, hashedPassword: string): Promise<boolean> {
  return bcrypt.compare(password, hashedPassword);
}

export function generateToken(payload: JWTPayload): string {
  return jwt.sign(payload, JWT_SECRET, { expiresIn: '7d' });
}

export function verifyToken(token: string): JWTPayload | null {
  try {
    return jwt.verify(token, JWT_SECRET) as JWTPayload;
  } catch {
    return null;
  }
}

export async function authenticateAdmin(username: string, password: string) {
  try {
    const admin = await prisma.adminUser.findUnique({
      where: { username },
    });

    if (!admin) {
      return null;
    }

    const isValid = await verifyPassword(password, admin.password_hash);
    if (!isValid) {
      return null;
    }

    return {
      id: admin.id,
      username: admin.username,
      role: admin.role,
    };
  } catch (error) {
    console.error('Authentication error:', error);
    return null;
  }
}

export async function createAdminUser(username: string, password: string, role: 'ADMIN' | 'SUPER_ADMIN' = 'ADMIN') {
  const hashedPassword = await hashPassword(password);
  
  return prisma.adminUser.create({
    data: {
      username,
      password_hash: hashedPassword,
      role,
    },
  });
}