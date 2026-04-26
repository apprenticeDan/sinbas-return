use argon2::{ password_hash::{PasswordVerifier, SaltString, PasswordHasher, PasswordHash, }, Argon2, };
use rand::rngs::OsRng;

pub fn hash_password(pass: &str) -> Result<String, String> {
    let salt = SaltString::generate(&mut OsRng);
    let argon2 = Argon2::default();
    let hash = argon2
        .hash_password(pass.as_bytes(), &salt)
        .map_err(|e| e.to_string())?
        .to_string();

    Ok(hash)
}

pub fn verify_password(pass: &str, hash: &str) -> Result<bool, String> {
    let parsed_hash = PasswordHash::new(hash)
        .map_err(|e| e.to_string())?;
    let argon2 = Argon2::default();

    match argon2.verify_password(pass.as_bytes(), &parsed_hash) {
        Ok(_) => Ok(true),
        Err(_) => Ok(false),
    }
}
