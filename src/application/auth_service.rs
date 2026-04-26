use crate::domain::{user::User, user_error::UserError};
use crate::infrastructure::security::{hash_password, verify_password};
use super::auth_error::AuthError;

pub struct AuthService;

impl AuthService {
    pub fn register(id: u64, email: String, password: String,) -> Result<User, UserError> {
        if password.is_empty() { return Err(UserError::EmptyPassword); }

        let pass_hash = hash_password(&password)
            .map_err(|_| UserError::HashingFailed)?;

        User::create(id, email, pass_hash)
    }

    pub fn login(users: &[User], email: &str, password: &str,) -> Result<User, AuthError> {
        let user = users
            .iter()
            .find(|u| u.email.value() == email)
            .ok_or(AuthError::UserNotFound)?;

        let valid = verify_password(password, user.password_hash())
            .map_err(|_| AuthError::VerificationFailed)?;
        if !valid { return Err(AuthError::InvalidCredentials); }

        Ok(user.clone())
    }
}

